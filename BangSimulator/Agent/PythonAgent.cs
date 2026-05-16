using System.Diagnostics;
using System.Net.Sockets;
using BangSimulator.Agent.Model;
using BangSimulator.Game;
using MessagePack;
using Microsoft.Extensions.Configuration;
using NetMQ;
using NetMQ.Sockets;

namespace BangSimulator.Agent
{

    public class PythonAgent : IAgent, IDisposable
    {
        private static readonly int basePort = 5555;

        private static int agentNextId = 0;
        private static Mutex idMutex = new();

        private static int GetNextAgentId()
        {
            idMutex.WaitOne();
            int id = agentNextId;
            agentNextId++;
            idMutex.ReleaseMutex();
            return id;
        }

        private PairSocket socket;
        private Process pythonProcess;

        public PythonAgent()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("GameConfig.json", optional: false, reloadOnChange: true)
                .Build();

            bool pythonDebug = configuration.GetValue<bool>("PythonDebugConsole");


            int agentID = GetNextAgentId();
            int port = basePort + agentID;


            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"PythonScripts\\pyAgentProcess.py \"{port}\"",
                    UseShellExecute = false,
                    CreateNoWindow = !pythonDebug
                }
            };

            pythonProcess.Start();

            Thread.Sleep(1000);

            socket = new PairSocket();
            socket.Connect($"tcp://127.0.0.1:{port}");
        }

        public void GameOver(PlayerRole winingRole)
        {
            byte[] bytes = MessagePackSerializer.Serialize(
                new PythonAgentRequest { RequestType = PythonAgentRequestType.GameOver, PlayerRole = winingRole });

            socket.SendFrame(bytes);
        }

        public void Reset()
        {
            byte[] bytes = MessagePackSerializer.Serialize(
                new PythonAgentRequest { RequestType = PythonAgentRequestType.Reset });

            socket.SendFrame(bytes);
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            var availableActions = gameInfo.AvanableActions;
            var deckMemory = gameInfo.DeckMemory;
            var cardsOut = gameInfo.CardsOut;

            var pythonActions = new PythonAction[availableActions.Count];
            var pythonMemory = new PythonMemory[deckMemory.Length];
            var cardsOutArray = new int[cardsOut.Count];

            for (int i = 0; i < availableActions.Count; i++)
            {
                var action = availableActions[i];

                pythonActions[i] = new PythonAction
                {
                    Type = action.PlayedCard != null
                        ? (int)action.PlayedCard.Type
                        : -1,

                    PotencialTargets = action.PotencialTargets
                };
            }

            for (int i = 0; i < deckMemory.Length; i++)
            {
                var memory = deckMemory[i];

                pythonMemory[i] = new PythonMemory
                {
                    Type = (int)memory.plaied.Type,
                    PId = memory.pId,
                    TargetId = memory.targetId
                };
            }

            for (int i = 0; i < cardsOut.Count; i++)
            {
                cardsOutArray[i] = (int)cardsOut[i].Type;
            }


            var agentRequest = new PythonAgentRequest
            {
                RequestType = PythonAgentRequestType.Step,
                PlayerRole = gameInfo.PlayerRole,

                ScherifId = gameInfo.ScherifId,
                PlayerHelth = gameInfo.PlayerHelth,
                GamePlayerLifes = gameInfo.GamePlayerLifes,

                AvanableActions = pythonActions,
                DeckMemory = pythonMemory,
                CardsOut = cardsOutArray
            };

            byte[] bytes = MessagePackSerializer.Serialize(agentRequest);

            socket.SendFrame(bytes);

            byte[] responseBytes = socket.ReceiveFrameBytes();

            var response = MessagePackSerializer.Deserialize<PythonAgentResponse>(responseBytes);

            Card? playedCard = null;

            if (response.Type != -1)
            {
                var targetType = (CardBangType)response.Type;

                for (int i = 0; i < availableActions.Count; i++)
                {
                    var action = availableActions[i];

                    if (action.PlayedCard?.Type == targetType)
                    {
                        playedCard = action.PlayedCard;
                        break;
                    }
                }
            }

            return new AgentAction
            {
                PlayedCard = playedCard,
                target = response.TargetId
            };
        }

        public void Dispose()
        {
            socket?.Dispose();

            if (!pythonProcess.HasExited)
            {
                pythonProcess.Kill();
            }

            pythonProcess.Dispose();
        }
    }




    [MessagePackObject]
    public class  PythonAgentResponse 
    {
        [Key(0)]
        public int Type { get; set; }

        [Key(1)]
        public int TargetId { get; set; }
    }

    [MessagePackObject]
    public class PythonAgentRequest
    {
        [Key("requestType")]
        public PythonAgentRequestType RequestType { get; set; }

        [Key("playerRole")]
        public PlayerRole PlayerRole { get; set; }

        [Key("scherifId")]
        public int ScherifId { get; set; }

        [Key("playerHelth")]
        public int PlayerHelth { get; set; }

        [Key("gamePlayerLifes")]
        public int[] GamePlayerLifes { get; set; } = [];

        [Key("avanableActions")]
        public PythonAction[] AvanableActions { get; set; } = [];

        [Key("deckMemory")]
        public PythonMemory[] DeckMemory { get; set; } = [];

        [Key("cardsOut")]
        public int[] CardsOut { get; set; } = [];
    }

    [MessagePackObject]
    public class PythonAction
    {
        [Key(0)]
        public int Type { get; set; }
        
        [Key(1)]
        public int[] PotencialTargets { get; set; } = [];
    }

    [MessagePackObject]
    public class PythonMemory
    {
        [Key(0)]
        public int Type { get; set; }
        [Key(1)]
        public int PId { get; set; }
        [Key(2)]
        public int TargetId { get; set; }
    }

    public enum PythonAgentRequestType
    {
        Step,
        Reset,
        GameOver
    }
}
