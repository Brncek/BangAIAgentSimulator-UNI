using System.Diagnostics;
using System.Net.Sockets;
using BangSimulator.Agent.Model;
using BangSimulator.Game;
using MessagePack;
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
            int agentID = GetNextAgentId();
            int port = basePort + agentID;

            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"PythonScripts\\pyAgentProcess.py \"{port}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
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
                new PythonAgentRequest { RequestType = PythonAgentRequestType.GameOver, playerRole = winingRole });

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
            PythonAgentRequest agentRequest = new PythonAgentRequest();
            agentRequest.RequestType = PythonAgentRequestType.Step;
            agentRequest.playerRole = gameInfo.PlayerRole;
            agentRequest.ScherifId = gameInfo.ScherifId;
            agentRequest.PlayerHelth = gameInfo.PlayerHelth;
            agentRequest.GamePlayerLifes = gameInfo.GamePlayerLifes;
            agentRequest.AvanableActions = gameInfo.AvanableActions.Select(a => new PythonAction
            {
                Type = a.PlayedCard != null ? (int)a.PlayedCard.Type : -1,
                PotencialTargets = a.PotencialTargets
            }).ToArray();

            agentRequest.DeckMemory = gameInfo.DeckMemory.Select(m => new PythonMemory
            {
                Type = (int)m.plaied.Type,
                PId = m.pId,
                TargetId = m.targetId
            }).ToArray();


            byte[] bytes = MessagePackSerializer.Serialize(agentRequest);

            socket.SendFrame(bytes);

            byte[] responseBytes = socket.ReceiveFrameBytes();

            var response = MessagePackSerializer.Deserialize<PythonAgentResponse>(responseBytes);

            AgentAction action = new AgentAction
            {
                PlayedCard = response.Type != -1 ? gameInfo.AvanableActions.FirstOrDefault(a => a.PlayedCard!.Type == (CardBangType)response.Type)?.PlayedCard : null,
                target = response.TargetId
            };

            return action;
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
        [Key(0)]
        public PythonAgentRequestType RequestType { get; set; } 

        [Key(1)]
        public PlayerRole playerRole { get; set; }

        [Key(2)]
        public int[] GamePlayerLifes { get; set; } = [];

        [Key(3)]
        public int ScherifId { get; set; }

        [Key(4)]
        public int PlayerHelth { get; set; } = 0;

        [Key(5)]
        public PythonAction[] AvanableActions { get; set; } = [];

        [Key(6)]
        public PythonMemory[] DeckMemory { get; set; } = [];

    }

    public class PythonAction
    {
        public int Type { get; set; }
        public int[] PotencialTargets { get; set; } = [];
    }

    public class PythonMemory
    {
        public int Type { get; set; }
        public int PId { get; set; }
        public int TargetId { get; set; }
    }

    public enum PythonAgentRequestType
    {
        Step,
        Reset,
        GameOver
    }
}
