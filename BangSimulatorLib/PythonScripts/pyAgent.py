import os
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.optim import Adam
from Enums import PlayerRole

# ── hyperparameters ───────────────────────────────────────────────────────────
BUFFER_SIZE = 2048
BATCH_SIZE  = 64
N_EPOCHS    = 10
GAMMA       = 0.99
LAM         = 0.95
CLIP_EPS    = 0.2
LR          = 3e-4
ENT_COEF    = 0.01
VF_COEF     = 0.5
MAX_GRAD    = 0.5


# ── network ───────────────────────────────────────────────────────────────────

class ActorCritic(nn.Module):
    """Shared-trunk actor-critic.
    obs_dim  – size of C# embededState vector (varies with player count)
    n_actions – size of C# embededCardMask  (varies with player count)
    """
    def __init__(self, obs_dim: int, n_actions: int):
        super().__init__()
        self.shared = nn.Sequential(
            nn.Linear(obs_dim, 256), nn.ReLU(),
            nn.Linear(256, 256),     nn.ReLU(),
        )
        self.actor  = nn.Linear(256, n_actions)
        self.critic = nn.Linear(256, 1)
        self._init_weights()

    def _init_weights(self):
        for m in self.modules():
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight, gain=np.sqrt(2))
                nn.init.zeros_(m.bias)
        nn.init.orthogonal_(self.actor.weight, gain=0.01)

    def forward(self, obs: torch.Tensor, mask: torch.BoolTensor | None = None):
        x      = self.shared(obs)
        logits = self.actor(x)
        if mask is not None:
            logits = logits.masked_fill(~mask, -1e8)
        dist  = torch.distributions.Categorical(logits=logits)
        value = self.critic(x).squeeze(-1)
        return dist, value


# ── rollout buffer ────────────────────────────────────────────────────────────

class RolloutBuffer:
    def __init__(self, obs_dim: int, n_actions: int):
        self.obs       = np.zeros((BUFFER_SIZE, obs_dim),    dtype=np.float32)
        self.actions   = np.zeros(BUFFER_SIZE,               dtype=np.int64)
        self.log_probs = np.zeros(BUFFER_SIZE,               dtype=np.float32)
        self.values    = np.zeros(BUFFER_SIZE,               dtype=np.float32)
        self.rewards   = np.zeros(BUFFER_SIZE,               dtype=np.float32)
        self.dones     = np.zeros(BUFFER_SIZE,               dtype=np.float32)
        self.masks     = np.zeros((BUFFER_SIZE, n_actions),  dtype=bool)
        self.pos = 0

    def add(self, obs, action, log_prob, value, reward, done, mask):

        if (self.pos == BUFFER_SIZE):
            self.pos = 0

        self.obs[self.pos]       = obs
        self.actions[self.pos]   = action
        self.log_probs[self.pos] = log_prob
        self.values[self.pos]    = value
        self.rewards[self.pos]   = reward
        self.dones[self.pos]     = done
        self.masks[self.pos]     = mask
        self.pos += 1

    def is_ready(self) -> bool:
        return self.pos >= BUFFER_SIZE

    def reset(self):
        self.pos = 0


# ── GAE ───────────────────────────────────────────────────────────────────────

def compute_gae(rewards, values, dones):
    advantages = np.zeros_like(rewards)
    last_gae   = 0.0
    next_val   = 0.0
    for t in reversed(range(len(rewards))):
        nonterminal   = 1.0 - dones[t]
        delta         = rewards[t] + GAMMA * next_val * nonterminal - values[t]
        last_gae      = delta + GAMMA * LAM * nonterminal * last_gae
        advantages[t] = last_gae
        next_val      = values[t]
    returns = advantages + values
    return advantages.astype(np.float32), returns.astype(np.float32)


# ── agent ─────────────────────────────────────────────────────────────────────

class pyAgent:
    def __init__(self):
        # Network is created lazily on the first Step() call because obs_dim
        # and n_actions depend on the player count configured in C#.
        self._policy    : ActorCritic   | None = None
        self._optimizer : torch.optim.Optimizer | None = None
        self._buffer    : RolloutBuffer | None = None

        # Pending transition: committed at the start of the NEXT Step()
        self._pending = None   # (obs, action, log_prob, value, mask)

        self._my_role      = -1
        self._is_eval      = False
        self._update_count = 0
        self._rewards      = []   # terminal reward per game (for GUI graphs)

        self._save_path      = None   # set by Save()
        self._load_path      = None   # remembered for lazy load
        self._last_reward_avg = 0.0   # best rolling-avg seen so far

    # ── lazy init ─────────────────────────────────────────────────────────────

    def _init_network(self, obs_dim: int, n_actions: int):
        self._policy    = ActorCritic(obs_dim, n_actions)
        self._optimizer = Adam(self._policy.parameters(), lr=LR, eps=1e-5)
        self._buffer    = RolloutBuffer(obs_dim, n_actions)

        if self._load_path and os.path.exists(self._load_path):
            try:
                self._policy.load_state_dict(
                    torch.load(self._load_path, weights_only=True))
                print(f"[PPO] loaded model from {self._load_path}")
            except Exception as e:
                print(f"[PPO] load failed: {e}")

    # ── IAgent interface ──────────────────────────────────────────────────────

    def Step(self, gameInfo: dict) -> list:
        # Use C#-built embedded state and action mask directly.
        # embededState  – float[] from GameInfo.Encode()
        # embededCardMask – float[] from GameInfo.BuildMask() (1.0 = allowed)
        obs  = np.array(gameInfo["embededState"],    dtype=np.float32)
        mask = np.array(gameInfo["embededCardMask"], dtype=np.float32) > 0.5

        self._my_role = gameInfo["playerRole"]

        # Lazy network creation – obs_dim and n_actions now known
        if self._policy is None:
            self._init_network(obs_dim=len(obs), n_actions=len(mask))

        # Commit the PREVIOUS step as a non-terminal transition
        if self._pending is not None:
            prev_obs, prev_action, prev_lp, prev_val, prev_mask = self._pending
            self._buffer.add(prev_obs, prev_action, prev_lp, prev_val,
                             reward=0.0, done=False, mask=prev_mask)
            if self._buffer.is_ready() and not self._is_eval:
                self._update()

        # Select action
        with torch.no_grad():
            obs_t  = torch.FloatTensor(obs).unsqueeze(0)
            mask_t = torch.BoolTensor(mask).unsqueeze(0)
            dist, value = self._policy(obs_t, mask_t)

            if self._is_eval:
                # Greedy: argmax over already-masked distribution probabilities
                flat_action = int(dist.probs.argmax(dim=-1).item())
            else:
                flat_action = int(dist.sample().item())

            log_prob = dist.log_prob(torch.tensor([flat_action]))

        self._pending = (obs, flat_action, float(log_prob.item()),
                         float(value.item()), mask)

        # Return MaskedActionIndex (index 2) so C# calls GameInfo.DecodeAction()
        # Format expected by PythonAgentResponse: [Type, TargetId, MaskedActionIndex, Rewards]
        # Type and TargetId are ignored by C# when MaskedActionIndex != -1
        return [0, 0, flat_action, [0.0]]

    def GameOver(self, winningRole: int):
        if self._pending is None:
            return

        my_team_won = (
            winningRole == self._my_role
            or (winningRole == int(PlayerRole.Sheriff)
                and self._my_role == int(PlayerRole.Deputy))
        )
        reward = 1.0 if my_team_won else -1.0
        self._rewards.append(reward)

        prev_obs, prev_action, prev_lp, prev_val, prev_mask = self._pending
        self._buffer.add(prev_obs, prev_action, prev_lp, prev_val,
                         reward=reward, done=True, mask=prev_mask)
        self._pending = None

        if self._buffer is not None and self._buffer.is_ready() and not self._is_eval:
            self._update()

        # Auto-save: only when save path was set and rolling avg improved
        if self._save_path:
            n = len(self._rewards)
            avg = sum(self._rewards[-100:]) / min(n, 100)
            if avg > self._last_reward_avg:
                self._last_reward_avg = avg
                try:
                    torch.save(self._policy.state_dict(), self._save_path)
                except Exception:
                    pass

    def Reset(self):
        self._pending = None

    def Rewards(self) -> list:
        return self._rewards if self._rewards else [0.0]

    def SetEval(self, isEval: bool):
        self._is_eval = isEval

    def Save(self, path: str):
        self._save_path = path
        if self._policy is not None:
            try:
                torch.save(self._policy.state_dict(), path)
                print(f"[PPO] saved model to {path}")
            except Exception as e:
                print(f"[PPO] save failed: {e}")

    def Load(self, path: str):
        self._load_path = path
        if self._policy is not None:
            try:
                self._policy.load_state_dict(
                    torch.load(path, weights_only=True))
                print(f"[PPO] loaded model from {path}")
            except Exception as e:
                print(f"[PPO] load failed: {e}")

    # ── PPO update ────────────────────────────────────────────────────────────

    def _update(self):
        n = self._buffer.pos

        obs_np     = self._buffer.obs[:n]
        actions_np = self._buffer.actions[:n]
        old_lp_np  = self._buffer.log_probs[:n]
        values_np  = self._buffer.values[:n]
        rewards_np = self._buffer.rewards[:n]
        dones_np   = self._buffer.dones[:n]
        masks_np   = self._buffer.masks[:n]

        advantages, returns = compute_gae(rewards_np, values_np, dones_np)
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        obs_t     = torch.FloatTensor(obs_np)
        actions_t = torch.LongTensor(actions_np)
        old_lp_t  = torch.FloatTensor(old_lp_np)
        adv_t     = torch.FloatTensor(advantages)
        returns_t = torch.FloatTensor(returns)
        masks_t   = torch.BoolTensor(masks_np)

        loss = torch.tensor(0.0)
        indices = np.arange(n)
        for _ in range(N_EPOCHS):
            np.random.shuffle(indices)
            for start in range(0, n, BATCH_SIZE):
                b = indices[start: start + BATCH_SIZE]

                dist, values = self._policy(obs_t[b], masks_t[b])
                new_lp  = dist.log_prob(actions_t[b])
                entropy = dist.entropy().mean()

                ratio      = torch.exp(new_lp - old_lp_t[b])
                adv_b      = adv_t[b]
                loss_clip  = -torch.min(
                    ratio * adv_b,
                    torch.clamp(ratio, 1 - CLIP_EPS, 1 + CLIP_EPS) * adv_b
                ).mean()
                loss_value = F.mse_loss(values, returns_t[b])
                loss       = loss_clip + VF_COEF * loss_value - ENT_COEF * entropy

                self._optimizer.zero_grad()
                loss.backward()
                nn.utils.clip_grad_norm_(self._policy.parameters(), MAX_GRAD)
                self._optimizer.step()

        self._update_count += 1
        if self._update_count % 10 == 0:
            print(f"[PPO] update #{self._update_count}  "
                  f"loss={loss.item():.4f}  "
                  f"mean_reward={rewards_np.sum() / max(dones_np.sum(), 1):.3f}")

        self._buffer.reset()