using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using DiscordRPC;
using Newtonsoft.Json.Linq;

namespace RoRPC
{
    public class AppContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _gameIconMenuItem;
        private ToolStripMenuItem _exitButton;
        private ToolStripMenuItem _githubLinkMenuItem;

        private DiscordRpcClient _client;
        private Process _roblox;

        public AppContext()
        {
            InitializeAppContextComponent();
        }

        private void InitializeAppContextComponent()
        {
            _contextMenu = new ContextMenuStrip();
            _gameIconMenuItem = new ToolStripMenuItem("Default game icon")
            {
                DropDownItems =
                {
                    new ToolStripMenuItem("Shiny", null, gameIconMenuItem_DropDownItemsClick) {Checked = true},
                    new ToolStripMenuItem("Red", null, gameIconMenuItem_DropDownItemsClick),
                    new ToolStripMenuItem("Old \'R\' Logo", null, gameIconMenuItem_DropDownItemsClick)
                }
            };
            _exitButton = new ToolStripMenuItem("Quit", null, exitButton_Click);
            _githubLinkMenuItem = new ToolStripMenuItem("GitHub", null, githubLinkMenuItem_Click);

            _contextMenu.Items.Add(_gameIconMenuItem);
            _contextMenu.Items.Add(_exitButton);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(new ToolStripLabel("RoRPC by sitiom"));
            _contextMenu.Items.Add(_githubLinkMenuItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon(Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application, 40, 40),
                ContextMenuStrip = _contextMenu,
                Text = "RoRPC",
                Visible = true
            };
            _notifyIcon.MouseClick += notifyIcon_MouseClick;

            _client = new DiscordRpcClient("803859459196715028");
            _client.Initialize();
            _notifyIcon.ShowBalloonTip(5, "Welcome to RoRPC!", "Connecting to Discord",
                ToolTipIcon.Info);
            _client.OnReady += async (_, _) =>
            {
                _notifyIcon.ShowBalloonTip(5, "RoRPC is ready", $"Hi there, {_client.CurrentUser.Username}!",
                    ToolTipIcon.Info);
                await Start();
            };
        }

        private void gameIconMenuItem_DropDownItemsClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            foreach (ToolStripMenuItem dropDownItem in _gameIconMenuItem.DropDownItems)
            {
                dropDownItem.Checked = dropDownItem == item;
            }

            if (_client.CurrentPresence is not null)
            {
                _client.UpdateLargeAsset(GetLargeImageKey());
            }
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            //Left click should also open the context menu
            if (e.Button != MouseButtons.Left) return;
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(_notifyIcon, null);
        }

        private void githubLinkMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/sitiom/RoRPC") { UseShellExecute = true });
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            _notifyIcon.Icon.Dispose();
            _notifyIcon.Dispose();
            _client.ClearPresence();
            _client.Dispose();
            Application.Exit();
        }
        private async void Roblox_Exited(object sender, EventArgs e)
        {
            _client.ClearPresence();
            await Start();
        }

        private async Task Start()
        {
            _roblox = await GetRobloxProcess();
            _roblox.EnableRaisingEvents = true;
            _roblox.Exited += Roblox_Exited;

            string placeId = GetPlaceId(_roblox);
            dynamic gameInfo =
                JObject.Parse(await HttpGetAsync($"https://api.roblox.com/marketplace/productinfo?assetId={placeId}"));

            RichPresence rp = new RichPresence
            {
                Details = gameInfo.Name ?? "(Unknown)",
                State = $"by {gameInfo.Creator.Name ?? "(Unknown)"}",
                Assets = new Assets
                {
                    SmallImageKey = "in-game",
                    SmallImageText = "https://github.com/sitiom/RoRPC",
                    LargeImageKey = GetLargeImageKey(),
                },
                Timestamps = new Timestamps(_roblox.StartTime.ToUniversalTime())
            };
            _client.SetPresence(rp);
        }

        private string GetLargeImageKey()
        {
            foreach (ToolStripMenuItem dropDownItem in _gameIconMenuItem.DropDownItems)
            {
                if (dropDownItem.Checked)
                {
                    switch (dropDownItem.Text)
                    {
                        case "Shiny":
                            return "logo_shiny";
                        case "Red":
                            return "logo_red";
                        case "Old \'R\' Logo":
                            return "logo_old";
                    }
                }
            }
            return null;
        }

        private static async Task<Process> GetRobloxProcess()
        {
            Process roblox = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
            while (roblox is null)
            {
                await Task.Delay(1000);
                roblox = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
            }
            return roblox;
        }

        private static string GetPlaceId(Process robloxProcess)
        {
            ProcessCommandLine.Retrieve(robloxProcess, out var cl);
            List<string> cmdLineArgs = ProcessCommandLine.CommandLineToArgs(cl).ToList();
            foreach (string arg in cmdLineArgs)
            {
                if (!Uri.IsWellFormedUriString(arg, UriKind.Absolute)) continue;
                Uri uri = new Uri(arg);
                string placeId = HttpUtility.ParseQueryString(uri.Query).Get("placeId");
                if (placeId is null) continue;
                return placeId;
            }
            return null;
        }

        private static async Task<string> HttpGetAsync(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            await using Stream stream = response.GetResponseStream();
            using StreamReader reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
