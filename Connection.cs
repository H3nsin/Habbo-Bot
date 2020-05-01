using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sulakore.Communication;
using Sulakore.Crypto;
using Sulakore.Protocol;

namespace KClient
{
    public class Connection
    {
        private readonly HKeyExchange _keyExchange;
        private RC4 _crypto;

        private readonly string _sso;

        public bool IsConnected;

        private readonly Random _rand;

        private bool _isWalkingAround;
        private int _tentativas;
        public int Id;

        private readonly MainFrm _main;
        private readonly string _proxyServer;
        private readonly int _proxyPort;

        private const int Exponent = 65537;
        private const string Modulus =
            "e052808c1abef69a1a62c396396b85955e2ff522f5157639fa6a19a98b54e0e4d6e44f44c4c0390fee8ccf642a22b6d46d7228b10e34ae6fffb61a35c11333780af6dd1aaafa7388fa6c65b51e8225c6b57cf5fbac30856e896229512e1f9af034895937b2cb6637eb6edf768c10189df30c10d8a3ec20488a198063599ca6ad";

        private HNode _server; 

        public Connection(string sso, MainFrm main, int id, string server = null, int port = 0)
        {
            _sso = sso;
            _main = main;
            Id = id;
            _keyExchange = new HKeyExchange(Exponent, Modulus);
            _rand = new Random();
            _tentativas = 0;

            _proxyServer = server;
            _proxyPort = port;
        }

        public async void Connect()
        {
            //if(_proxyServer != null)
            ////_server.SOCKS5EndPoint = new IPEndPoint(IPAddress.Parse(_proxyServer), _proxyPort);

            /*try
            {
                Socket Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Client.NoDelay = true;
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("3.210.175.39"), 38101);
                IAsyncResult result = Client.BeginConnect(endPoint, null, null);
                await Task.Factory.FromAsync(result, Client.EndConnect).ConfigureAwait(false);
            }
            catch( Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }*/
            
            _server = HNode.ConnectNewAsync("game-us.habbo.com", 38101).Result;
            while (_server == null);

            if (_server.IsConnected)
                _main.LogSucess("Connected to habbo server");

            HMessage releaseVerMsg = new HMessage(HMessage.ToBytes("{l}{u:4000}{s:PRODUCTION-202004272205-912004688}{s:FLASH}{i:1}{i:0}"));
            await _server.SendPacketAsync(releaseVerMsg);
            HMessage initCryptoMsg = new HMessage(HMessage.Construct(178, HMessage.ToBytes("[0][0][0][2][0]²")));
            await _server.SendPacketAsync(initCryptoMsg);
            //await _server.SendPacketAsync(Headers.ReleaseVersion, Headers.Variables.Production, "FLASH", 1, 0);
            //await _server.SendPacketAsync(Headers.InitCrypto);

            HMessage packet = await _server.ReceivePacketAsync().ConfigureAwait(false);
            while (packet == null);
            HandlePacket(packet);
            //Listen();
        }

        private TcpClient server = new TcpClient();

        async Task Listen()
        {
            try
            {
                await server.ConnectAsync(IPAddress.Parse("3.210.175.39"), 38101);

                if (server.Connected)
                {
                    MessageBox.Show("Connected");
                    NetworkStream stream = server.GetStream();

                    while (server.Connected)
                    {
                        byte[] buffer = new byte[server.ReceiveBufferSize];
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read > 0)
                        {
                            // you have received a message, do something with it
                            MessageBox.Show("Received a packet");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // display the error message or whatever
                MessageBox.Show(ex.ToString());
                server.Close();
            }
        }

        private async void HandlePacket(HMessage hmessage)
        {
            try
            {
                if (hmessage == null)
                    return;

                _main.LogInfo("Incoming packet: " + hmessage.Header);

                if (hmessage.Header == Headers.Ping)
                {
                    SendPong();
                }
                else if (hmessage.Header == Headers.InGenerateSecretKey)
                {
                    GenerateSecretKey(hmessage.ReadString());
                }
                else if (hmessage.Header == 3566)
                {
                    _main.LogInfo("VerifyPrimes");
                    VerifyPrimes(hmessage.ReadString(), hmessage.ReadString());
                }

                HandlePacket(await _server.ReceivePacketAsync());
            }
            catch
            {
                Disconnect();
            }
        }

        public void GenerateSecretKey(string publicKey)
        {
            _crypto = new RC4(_keyExchange.GetSharedKey(publicKey));
            SendToServer(_crypto.Parse(HMessage.Construct(Headers.ClientVariables, 401, Headers.Variables.ClientVariables_1, Headers.Variables.ClientVariables_2)));
            SendToServer(_crypto.Parse(HMessage.Construct(Headers.MachineID, GenMid(), "WIN/30,0,0,154")));
            SendToServer(_crypto.Parse(HMessage.Construct(Headers.SSOTicket, _sso, _rand.Next(400, 4600))));
            SendToServer(_crypto.Parse(HMessage.Construct(Headers.RequestUserData)));
            IsConnected = true;
            _main.LogSucess($"[BOT {Id}] Connected");
        }

        private async void SendToServer(byte[] parse)
        {
            await _server.SendAsync(parse);
        }

        public void VerifyPrimes(string prime, string generator)
        {
            _keyExchange.VerifyDHPrimes(prime, generator);
            _keyExchange.Padding = PKCSPadding.RandomByte;
            _main.LogInfo("GenerateSecretKey: " + Headers.OutGenerateSecretKey);
            SendToServer(HMessage.Construct(Headers.OutGenerateSecretKey, _keyExchange.GetPublicKey()));
            //SendToServer(HMessage.Construct(Headers.OutGe))
        }

        private void SendPong()
        {
            SendToServerCrypto(HMessage.Construct(Headers.Pong));
        }

        private async void SendToServerCrypto(byte[] data)
        {
            if (CanEncrypt())
                _crypto.RefParse(data);
            await _server.SendAsync(data);
        }

        private bool CanEncrypt()
        {
            return _crypto != null;
        }

        public void Disconnect()
        {
            IsConnected = false;

            if (_tentativas < 3)
            {
                _main.LogWarning($"[BOT {Id}] Reconnecting ({_tentativas + 1}/3)");
                Connect();
                _tentativas++;
                return;
            }

            _main.LogError($"[BOT {Id}] Disconnected");

            _main.RemoveBotFromLists(Id);
            Handler.Bots.Remove(this);
        }

        public void LoadRoom(int room)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RequestRoomLoad, room, "", -1));
        }

        public void Shout(string msg)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RoomUserShout, msg, 1));
        }

        public void ChangeClothes(string figureId)
        {
            SendToServerCrypto(HMessage.Construct(Headers.UserSaveLook, "M", figureId));
        }

        public void WalkTo(int x, int y)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RoomUserWalk, x, y));
        }

        public void Respect(int id)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RoomUserGiveRespect, id));
        }

        public void Sit(bool status)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RoomUserSit, status ? 1 : 0));
        }

        public void Scratch(int petId)
        {
            SendToServerCrypto(HMessage.Construct(Headers.ScratchPet, petId));
        }

        public void Dance(bool status)
        {
            SendToServerCrypto(HMessage.Construct(Headers.RoomUserDance, status ? 1 : 0));
        }

        public void WalkAround(bool status)
        {
            _isWalkingAround = status;
            if(_isWalkingAround)
                new Task(WalkAroundTask).Start();
        }

        private async void WalkAroundTask()
        {
            while (_isWalkingAround)
            {
                WalkTo(_rand.Next(1, 24), _rand.Next(1, 24));
                await Task.Delay(400);
            }
        }

        private string GenMid(int length = 32)
        {
            using (var rngProvider = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[length];
                rngProvider.GetBytes(bytes);

                using (var md5 = MD5.Create())
                {
                    var md5Hash = md5.ComputeHash(bytes);

                    var sb = new StringBuilder();
                    foreach (var data in md5Hash)
                    {
                        sb.Append(data.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
        }

    }
}
