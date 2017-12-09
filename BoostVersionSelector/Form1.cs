using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace BoostVersionSelector
{
    public partial class Form1 : Form
    {
        private Dictionary<string, Tuple<int, string>> _boostPaths { get; set; }

        public Form1()
        {
            InitializeComponent();

            var latestVersion = 0;
            var latestVersionNormal = new [] {0, 0, 0};
            // Check if new boost version available
            var reader = XmlReader.Create("http://www.boost.org/feed/downloads.rss");
            var feed = SyndicationFeed.Load(reader);
            reader.Close();
            foreach (var item in feed.Items)
            {
                var subject = item.Title.Text;
                var version = subject.Replace("Version ", "").Split('.').Select(int.Parse).ToArray();
                var hex = GetVersion(version);

                if (hex > latestVersion)
                {
                    latestVersionNormal = version;
                    latestVersion = hex;
                }
            }

            _boostPaths = new Dictionary<string, Tuple<int, string>>();
            foreach (var dir in Directory.EnumerateDirectories("E:\\boost", "boost_*"))
            {
                if (!File.Exists(Path.Combine(dir, "boost", "version.hpp")))
                    continue;

                var tokens = Path.GetFileName(dir).Split('_');
                var hex = GetVersion(tokens.Skip(1).Select(int.Parse).ToArray());
                var named = $"Version {tokens[1]}.{tokens[2]}.{tokens[3]} [{hex}]{(hex == latestVersion ? " (Latest)" : "")}";
                _boostPaths.Add(named, new Tuple<int, string>(hex, dir));
                comboBox1.Items.Add(named);
            }
            comboBox1.SelectedIndex = 0;

            if (latestVersion > _boostPaths.Max(x => x.Value.Item1))
                MessageBox.Show($@"New version available: {latestVersionNormal[0]}.{latestVersionNormal[1]}.{latestVersionNormal[2]} [{latestVersion}]!");
        }

        private int GetVersion(int[] version)
        {
            return version[0] * (16 ^ 2) + version[1] * (16 ^ 1) + version[2] * (16 ^ 0);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var selectedData = _boostPaths.FirstOrDefault(x => x.Key == (string) comboBox1.SelectedItem).Value;
            Environment.SetEnvironmentVariable("BOOST_ROOT", selectedData.Item2, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("BOOST_DIR", selectedData.Item2, EnvironmentVariableTarget.User);
            MessageBox.Show($@"Boost path was set to: {selectedData.Item2}");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button1.Enabled = false;
            Task.Factory.StartNew(() =>
            {
                var selectedData = _boostPaths.FirstOrDefault(x => x.Key == (string) comboBox1.SelectedItem).Value;

                var p = new Process();
                var info = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false
                };

                p.StartInfo = info;
                p.Start();

                using (var sw = p.StandardInput)
                {
                    if (sw.BaseStream.CanWrite)
                    {
                        sw.WriteLine("\"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Auxiliary\\Build\\vcvars64.bat\"");
                        sw.WriteLine($"cd \"{selectedData.Item2}\"");
                        sw.WriteLine(Path.GetPathRoot(selectedData.Item2)?.Replace("\\", ""));
                        if (!File.Exists(Path.Combine(selectedData.Item2, "b2.exe")))
                            sw.WriteLine("bootstrap.bat");
                        sw.WriteLine($"b2 toolset=msvc-14.0 link=static threading=multi --without-python --without-fiber -sZLIB_SOURCE=\"E:\\zlib\" address-model=64 --build-type=complete stage -j{Environment.ProcessorCount}");
                    }
                }
                p.WaitForExit();
                button2.Enabled = true;
                button1.Enabled = true;
                MessageBox.Show(@"Boost has been built.");
            });
        }

        private void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = false,
                UseShellExecute = false
            };

            var process = Process.Start(processInfo);
            process?.WaitForExit();
            process?.Close();
        }
    }
}
