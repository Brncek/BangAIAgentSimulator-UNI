using System.Diagnostics;
using System.Net.Sockets;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;
using MessagePack;
using Microsoft.Extensions.Configuration;

using System.IO.Pipes;
using MessagePack.Formatters;

namespace BangSimulatorLib.Agent
{

    public class PythonAgent : IAgent, IDisposable
    {
        private Process pythonProcess;

        private NamedPipeServerStream pipe;

        public PythonAgent()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("GameConfig.json", optional: false, reloadOnChange: true)
                .Build();

            bool pythonDebug = configuration.GetValue<bool>("PythonDebugConsole");


            string pipeName =$"PyAgentPipe_{Guid.NewGuid()}";

            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.None);


            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"PythonScripts\\pyAgentProcess.py {pipeName}",
                    UseShellExecute = false,
                    CreateNoWindow = !pythonDebug
                }
            };
            pythonProcess.Start();

            pipe.WaitForConnection();

            Thread.Sleep(1000);
        }

        public void GameOver(PlayerRole winingRole)
        {
            SendData(new PythonAgentRequest { RequestType = PythonAgentRequestType.GameOver, PlayerRole = winingRole });
        }

        public void Reset()
        {
            SendData(new PythonAgentRequest { RequestType = PythonAgentRequestType.Reset });
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

            var embeded = gameInfo.Encode();

            var agentRequest = new PythonAgentRequest
            {
                RequestType = PythonAgentRequestType.Step,
                PlayerRole = gameInfo.PlayerRole,

                ScherifId = gameInfo.ScherifId,
                PlayerHelth = gameInfo.PlayerHelth,
                GamePlayerLifes = gameInfo.GamePlayerLifes,

                AvanableActions = pythonActions,
                DeckMemory = pythonMemory,
                CardsOut = cardsOutArray,

                EmbededState = embeded.State,
                EmbededCardMask = embeded.Mask
            };

            SendData(agentRequest);

            var response = ReceveData();

            Card? playedCard = null;
            CardBangType? targetType = null;
            int targetId = -1;

            if (response.MaskedActionIndex != -1)
            {
                var output = gameInfo.DecodeAction(response.MaskedActionIndex);

                if (output.EndTurn)
                {
                    return new AgentAction { PlayedCard = null, target = -1 };
                }

                targetType = output.Card;
                targetId = output.Target;
            }
            else
            {
                if (response.Type != -1)
                {
                    targetType = (CardBangType)response.Type;
                    targetId = response.TargetId;
                }
            }

            if (targetType != null)
            {
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
                target = targetId
            };
        }

        public void Dispose()
        {
            if (!pythonProcess.HasExited)
            {
                pythonProcess.Kill();
            }
            pythonProcess.Dispose();

            pipe.Close();
            pipe.Dispose();
        }

        private void SendData(PythonAgentRequest data)
        {
            byte[] bytes = MessagePackSerializer.Serialize(data);
            byte[] sizeBytes = BitConverter.GetBytes(bytes.Length);


            pipe.Write(sizeBytes, 0, sizeBytes.Length);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
        }

        private PythonAgentResponse ReceveData()
        {
            byte[] responseSizeBytes = new byte[4];
            ReadExact( pipe, responseSizeBytes, 4);

            int responseSize = BitConverter.ToInt32(responseSizeBytes);
            byte[] responseBytes = new byte[responseSize];

            ReadExact(pipe, responseBytes, responseSize);

            return MessagePackSerializer.Deserialize<PythonAgentResponse>(responseBytes);
        }

        private void ReadExact( Stream stream, byte[] buffer, int size)
        {
            int offset = 0;

            while (offset < size)
            {
                int read = stream.Read(
                    buffer,
                    offset,
                    size - offset);

                if (read == 0)
                {
                    throw new Exception("Pipe closed");
                }

                offset += read;
            }
        }
    }




    [MessagePackObject]
    public class  PythonAgentResponse 
    {
        [Key(0)]
        public int Type { get; set; }

        [Key(1)]
        public int TargetId { get; set; }

        [Key(2)]
        public int MaskedActionIndex { get; set; } = -1;
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

        [Key("embededState")]
        public float[] EmbededState { get; set; } = [];

        [Key("embededCardMask")]
        public float[] EmbededCardMask { get; set; } = [];
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
