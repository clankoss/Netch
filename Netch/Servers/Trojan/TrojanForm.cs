using Netch.Forms;

namespace Netch.Servers
{
    public class TrojanForm : ServerForm
    {
        public TrojanForm(TrojanServer? server = default)
        {
            server ??= new TrojanServer();
            Server = server;
            CreateTextBox("Password", "Password", s => !string.IsNullOrWhiteSpace(s), s => server.Password = s, server.Password);
            CreateTextBox("Host", "TLS SNI", s => true, s => server.Host = s, server.Host);
        }

        protected override string TypeName { get; } = "Trojan";
    }
}