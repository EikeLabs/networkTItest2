using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AdvancedNetworkDashboard
{
    public class MainForm : Form
    {
        // UI Layout Components
        private Panel pnlTop;
        private Label lblTitle;
        private Label lblAdapterInfo;

        private TabControl mainTabControl;
        private TabPage tabOverview;
        private TabPage tabDetails;
        private TabPage tabTools;
        private TabPage tabArp;

        // Tab Overview Controls
        private GroupBox grpMap;
        private Panel boxPc;
        private Panel boxRouter;
        private Panel boxInternet;
        private Label lblStatusPc;
        private Label lblStatusRouter;
        private Label lblStatusInternet;

        private GroupBox grpSpeed;
        private Label lblDownload;
        private Label lblUpload;
        private Label lblTotalReceived;
        private Label lblTotalSent;
        private PictureBox picGraph;

        // Tab Details Controls
        private ListView lstDetails;

        // Tab Tools Controls
        private Button btnPingRouter;
        private Button btnPingCustom;
        private TextBox txtCustomHost;
        private Button btnScanPorts;
        private Button btnTraceroute;
        private Button btnFlushDns;
        private RichTextBox txtLog;

        // Tab ARP Controls
        private ListView lstArp;
        private Button btnRefreshArp;

        // Monitoring and Core Data
        private System.Windows.Forms.Timer updateTimer;
        private NetworkInterface activeInterface;
        private long lastBytesReceived = 0;
        private long lastBytesSent = 0;
        private string routerIp = "192.168.1.1";

        // Graphing Engine (Real-Time Speed Graph)
        private List<int> speedHistory = new List<int>();
        private const int MaxGraphPoints = 40;

        public MainForm()
        {
            this.Text = "Central Ultra-Avançada de Diagnóstico de Rede - Windows 7 / .NET 4.0";
            this.Size = new Size(880, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(238, 242, 245);

            BuildInterface();
            InitNetworkData();
        }

        private void BuildInterface()
        {
            // --- TOP HEADER BAR ---
            pnlTop = new Panel();
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Height = 65;
            pnlTop.BackColor = Color.FromArgb(41, 128, 185);
            this.Controls.Add(pnlTop);

            lblTitle = new Label();
            lblTitle.Text = "PAINEL DE CONTROLE E MONITORAMENTO DE REDE";
            lblTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(15, 12);
            lblTitle.AutoSize = true;
            pnlTop.Controls.Add(lblTitle);

            lblAdapterInfo = new Label();
            lblAdapterInfo.Text = "Placa Ativa: Detectando adaptador...";
            lblAdapterInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblAdapterInfo.ForeColor = Color.FromArgb(220, 230, 242);
            lblAdapterInfo.Location = new Point(16, 38);
            lblAdapterInfo.AutoSize = true;
            pnlTop.Controls.Add(lblAdapterInfo);

            // --- TAB CONTROL CONTAINER ---
            mainTabControl = new TabControl();
            mainTabControl.Location = new Point(12, 75);
            mainTabControl.Size = new Size(840, 500);
            mainTabControl.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            this.Controls.Add(mainTabControl);

            // Tab 1: Vision / Overview
            tabOverview = new TabPage("Visão Geral & Gráficos");
            tabOverview.BackColor = Color.FromArgb(245, 247, 250);
            mainTabControl.TabPages.Add(tabOverview);

            // Tab 2: Hardware Details
            tabDetails = new TabPage("Detalhes da Placa");
            tabDetails.BackColor = Color.FromArgb(245, 247, 250);
            mainTabControl.TabPages.Add(tabDetails);

            // Tab 3: Diagnostics Tools
            tabTools = new TabPage("Ferramentas de Diagnóstico");
            tabTools.BackColor = Color.FromArgb(245, 247, 250);
            mainTabControl.TabPages.Add(tabTools);

            // Tab 4: ARP Table
            tabArp = new TabPage("Dispositivos ARP");
            tabArp.BackColor = Color.FromArgb(245, 247, 250);
            mainTabControl.TabPages.Add(tabArp);

            // ==========================================
            // BUILD TAB 1: OVERVIEW
            // ==========================================
            grpMap = new GroupBox();
            grpMap.Text = " Topologia da Conexão Local ";
            grpMap.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            grpMap.Location = new Point(15, 15);
            grpMap.Size = new Size(805, 125);
            tabOverview.Controls.Add(grpMap);

            boxPc = CreateNodeBox("Meu Computador", "Host Local", out lblStatusPc);
            boxPc.Location = new Point(25, 28);
            grpMap.Controls.Add(boxPc);

            Label arrow1 = CreateArrowLabel("========>");
            arrow1.Location = new Point(235, 58);
            grpMap.Controls.Add(arrow1);

            boxRouter = CreateNodeBox("Roteador / Gateway", "Rede Local (LAN)", out lblStatusRouter);
            boxRouter.Location = new Point(315, 28);
            grpMap.Controls.Add(boxRouter);

            Label arrow2 = CreateArrowLabel("========>");
            arrow2.Location = new Point(525, 58);
            grpMap.Controls.Add(arrow2);

            boxInternet = CreateNodeBox("Provedor / Internet", "Mundo Exterior (WAN)", out lblStatusInternet);
            boxInternet.Location = new Point(605, 28);
            grpMap.Controls.Add(boxInternet);

            grpSpeed = new GroupBox();
            grpSpeed.Text = " Tráfego em Tempo Real ";
            grpSpeed.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            grpSpeed.Location = new Point(15, 150);
            grpSpeed.Size = new Size(380, 300);
            tabOverview.Controls.Add(grpSpeed);

            lblDownload = CreateStatLabel("Download Atual: 0.00 KB/s", Color.Navy, 30);
            lblUpload = CreateStatLabel("Upload Atual: 0.00 KB/s", Color.DarkGreen, 60);
            lblTotalReceived = CreateStatLabel("Total Recebido: 0 MB", Color.Black, 90);
            lblTotalSent = CreateStatLabel("Total Enviado: 0 MB", Color.Black, 120);

            grpSpeed.Controls.Add(lblDownload);
            grpSpeed.Controls.Add(lblUpload);
            grpSpeed.Controls.Add(lblTotalReceived);
            grpSpeed.Controls.Add(lblTotalSent);

            // PictureBox for Real-Time Speed Graph
            picGraph = new PictureBox();
            picGraph.Location = new Point(20, 155);
            picGraph.Size = new Size(340, 125);
            picGraph.BackColor = Color.Black;
            picGraph.Paint += PicGraph_Paint;
            grpSpeed.Controls.Add(picGraph);

            // Log na visão geral
            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Histórico de Eventos Globais:";
            lblLogTitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblLogTitle.Location = new Point(410, 150);
            lblLogTitle.AutoSize = true;
            tabOverview.Controls.Add(lblLogTitle);

            txtLog = new RichTextBox();
            txtLog.Location = new Point(410, 175);
            txtLog.Size = new Size(410, 275);
            txtLog.ReadOnly = true;
            txtLog.BackColor = Color.FromArgb(20, 20, 20);
            txtLog.ForeColor = Color.LimeGreen;
            txtLog.Font = new Font("Consolas", 8.5f);
            tabOverview.Controls.Add(txtLog);

            // ==========================================
            // BUILD TAB 2: DETAILS
            // ==========================================
            lstDetails = new ListView();
            lstDetails.Location = new Point(15, 15);
            lstDetails.Size = new Size(805, 435);
            lstDetails.View = View.Details;
            lstDetails.FullRowSelect = true;
            lstDetails.GridLines = true;
            lstDetails.Columns.Add("Propriedade de Rede", 250);
            lstDetails.Columns.Add("Valor Detectado", 530);
            tabDetails.Controls.Add(lstDetails);

            // ==========================================
            // BUILD TAB 3: TOOLS
            // ==========================================
            GroupBox grpToolsPanel = new GroupBox();
            grpToolsPanel.Text = " Testes de Latência e Serviços ";
            grpToolsPanel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            grpToolsPanel.Location = new Point(15, 15);
            grpToolsPanel.Size = new Size(805, 435);
            tabTools.Controls.Add(grpToolsPanel);

            btnPingRouter = new Button();
            btnPingRouter.Text = "Ping Gateway Local";
            btnPingRouter.Location = new Point(20, 35);
            btnPingRouter.Size = new Size(180, 35);
            btnPingRouter.Click += (s, e) => ActionPingRouter();
            grpToolsPanel.Controls.Add(btnPingRouter);

            txtCustomHost = new TextBox();
            txtCustomHost.Text = "8.8.8.8";
            txtCustomHost.Location = new Point(220, 41);
            txtCustomHost.Size = new Size(140, 23);
            grpToolsPanel.Controls.Add(txtCustomHost);

            btnPingCustom = new Button();
            btnPingCustom.Text = "Ping IP/Host Custom";
            btnPingCustom.Location = new Point(370, 35);
            btnPingCustom.Size = new Size(180, 35);
            btnPingCustom.Click += (s, e) => ActionPingCustom(txtCustomHost.Text);
            grpToolsPanel.Controls.Add(btnPingCustom);

            btnScanPorts = new Button();
            btnScanPorts.Text = "Testar Portas de Serviço";
            btnScanPorts.Location = new Point(20, 85);
            btnScanPorts.Size = new Size(180, 35);
            btnScanPorts.Click += (s, e) => ActionScanServices();
            grpToolsPanel.Controls.Add(btnScanPorts);

            btnTraceroute = new Button();
            btnTraceroute.Text = "Executar Traceroute";
            btnTraceroute.Location = new Point(220, 85);
            btnTraceroute.Size = new Size(180, 35);
            btnTraceroute.Click += (s, e) => ActionTraceroute(txtCustomHost.Text);
            grpToolsPanel.Controls.Add(btnTraceroute);

            btnFlushDns = new Button();
            btnFlushDns.Text = "Limpar Cache DNS";
            btnFlushDns.Location = new Point(420, 85);
            btnFlushDns.Size = new Size(180, 35);
            btnFlushDns.Click += (s, e) => ActionFlushDns();
            grpToolsPanel.Controls.Add(btnFlushDns);

            // ==========================================
            // BUILD TAB 4: ARP
            // ==========================================
            btnRefreshArp = new Button();
            btnRefreshArp.Text = "Atualizar Tabela ARP";
            btnRefreshArp.Location = new Point(15, 15);
            btnRefreshArp.Size = new Size(160, 30);
            btnRefreshArp.Click += (s, e) => LoadArpTable();
            tabArp.Controls.Add(btnRefreshArp);

            lstArp = new ListView();
            lstArp.Location = new Point(15, 55);
            lstArp.Size = new Size(805, 395);
            lstArp.View = View.Details;
            lstArp.FullRowSelect = true;
            lstArp.GridLines = true;
            lstArp.Columns.Add("Endereço IP", 200);
            lstArp.Columns.Add("Endereço Físico (MAC)", 250);
            lstArp.Columns.Add("Tipo de Entrada", 150);
            tabArp.Controls.Add(lstArp);

            // --- FOOTER ---
            Label lblFooter = new Label();
            lblFooter.Text = "Central de Diagnóstico de Rede | Compatibilidade: Windows 7, 8, 10 e 11 (csc.exe Native)";
            lblFooter.Font = new Font("Segoe UI", 8, FontStyle.Italic);
            lblFooter.ForeColor = Color.Gray;
            lblFooter.Location = new Point(15, 582);
            lblFooter.AutoSize = true;
            this.Controls.Add(lblFooter);

            // Timers & Graph Initialization
            for (int i = 0; i < MaxGraphPoints; i++) speedHistory.Add(0);

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += UpdateMetrics;
        }

        private Panel CreateNodeBox(string title, string subtitle, out Label statusLabel)
        {
            Panel pnl = new Panel();
            pnl.Size = new Size(190, 80);
            pnl.BorderStyle = BorderStyle.FixedSingle;
            pnl.BackColor = Color.White;

            Label t = new Label();
            t.Text = title;
            t.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            t.Location = new Point(8, 8);
            t.Size = new Size(175, 18);
            pnl.Controls.Add(t);

            Label sub = new Label();
            sub.Text = subtitle;
            sub.Font = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            sub.ForeColor = Color.Gray;
            sub.Location = new Point(8, 28);
            sub.Size = new Size(175, 16);
            pnl.Controls.Add(sub);

            statusLabel = new Label();
            statusLabel.Text = "Status: Await...";
            statusLabel.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            statusLabel.Location = new Point(8, 52);
            statusLabel.Size = new Size(175, 20);
            pnl.Controls.Add(statusLabel);

            return pnl;
        }

        private Label CreateArrowLabel(string text)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Consolas", 10, FontStyle.Bold);
            lbl.ForeColor = Color.Gray;
            lbl.Size = new Size(70, 20);
            return lbl;
        }

        private Label CreateStatLabel(string initialText, Color textColor, int yPos)
        {
            Label lbl = new Label();
            lbl.Text = initialText;
            lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lbl.ForeColor = textColor;
            lbl.Location = new Point(15, yPos);
            lbl.AutoSize = true;
            return lbl;
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { Log(msg); });
                return;
            }
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void InitNetworkData()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.Supports(NetworkInterfaceComponent.IPv4) &&
                    (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    activeInterface = adapter;
                    break;
                }
            }

            if (activeInterface != null)
            {
                lblAdapterInfo.Text = "Placa Ativa: " + activeInterface.Name + " (" + activeInterface.NetworkInterfaceType + ")";

                IPInterfaceProperties properties = activeInterface.GetIPProperties();
                foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        routerIp = gateway.Address.ToString();
                        break;
                    }
                }

                IPv4InterfaceStatistics stats = activeInterface.GetIPv4Statistics();
                lastBytesReceived = stats.BytesReceived;
                lastBytesSent = stats.BytesSent;

                lblStatusPc.Text = "PC: Online";
                lblStatusPc.ForeColor = Color.Green;

                lblStatusRouter.Text = "Gateway: " + routerIp;
                lblStatusRouter.ForeColor = Color.DarkBlue;

                lblStatusInternet.Text = "WAN: Conectado";
                lblStatusInternet.ForeColor = Color.Green;

                PopulateDetailsTab(properties);
                LoadArpTable();

                Log("Adaptador de Rede Ativo: " + activeInterface.Description);
                Log("Gateway Padrao Detectado: " + routerIp);

                updateTimer.Start();
            }
            else
            {
                lblAdapterInfo.Text = "Placa Ativa: Nenhuma rede ativa encontrada";
                lblStatusPc.Text = "PC: Desconectado";
                lblStatusPc.ForeColor = Color.Red;
                lblStatusRouter.Text = "Gateway: Indisponivel";
                lblStatusInternet.Text = "WAN: Indisponivel";
                Log("Erro Crítico: Nenhum adaptador IPv4 ativo foi detectado.");
            }
        }

        private void PopulateDetailsTab(IPInterfaceProperties properties)
        {
            lstDetails.Items.Clear();
            AddDetailItem("Nome da Interface", activeInterface.Name);
            AddDetailItem("Descrição do Hardware", activeInterface.Description);
            AddDetailItem("Tipo de Conexão", activeInterface.NetworkInterfaceType.ToString());
            AddDetailItem("Endereço Físico (MAC)", activeInterface.GetPhysicalAddress().ToString());
            AddDetailItem("Velocidade do Link", (activeInterface.Speed / 1000000) + " Mbps");
            AddDetailItem("Status Operacional", activeInterface.OperationalStatus.ToString());

            foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    AddDetailItem("Endereço IPv4", ip.Address.ToString());
                    if (ip.IPv4Mask != null)
                        AddDetailItem("Máscara de Sub-rede", ip.IPv4Mask.ToString());
                }
            }

            foreach (GatewayIPAddressInformation gw in properties.GatewayAddresses)
            {
                AddDetailItem("Gateway Padrão", gw.Address.ToString());
            }

            foreach (IPAddress dns in properties.DnsAddresses)
            {
                if (dns.AddressFamily == AddressFamily.InterNetwork)
                    AddDetailItem("Servidor DNS", dns.ToString());
            }

            // Compatível com C# 4.0 / .NET Framework 4.0
            IPv4InterfaceProperties ipv4Props = properties.GetIPv4Properties();
            string dhcpEnabled = (ipv4Props != null) ? ipv4Props.IsDhcpEnabled.ToString() : "N/A";
            AddDetailItem("DHCP Habilitado", dhcpEnabled);
        }

        private void AddDetailItem(string key, string value)
        {
            ListViewItem item = new ListViewItem(key);
            item.SubItems.Add(value);
            lstDetails.Items.Add(item);
        }

        private void UpdateMetrics(object sender, EventArgs e)
        {
            if (activeInterface == null) return;

            try
            {
                IPv4InterfaceStatistics stats = activeInterface.GetIPv4Statistics();

                long currentReceived = stats.BytesReceived;
                long currentSent = stats.BytesSent;

                long diffReceived = currentReceived - lastBytesReceived;
                long diffSent = currentSent - lastBytesSent;

                double downloadKB = diffReceived / 1024.0;
                double uploadKB = diffSent / 1024.0;

                double totalMbReceived = currentReceived / (1024.0 * 1024.0);
                double totalMbSent = currentSent / (1024.0 * 1024.0);

                lblDownload.Text = "Download Atual: " + downloadKB.ToString("F2") + " KB/s";
                lblUpload.Text = "Upload Atual: " + uploadKB.ToString("F2") + " KB/s";

                lblTotalReceived.Text = "Total Recebido: " + totalMbReceived.ToString("F1") + " MB";
                lblTotalSent.Text = "Total Enviado: " + totalMbSent.ToString("F1") + " MB";

                lastBytesReceived = currentReceived;
                lastBytesSent = currentSent;

                // Push to Graph
                speedHistory.Add((int)downloadKB);
                if (speedHistory.Count > MaxGraphPoints)
                    speedHistory.RemoveAt(0);

                picGraph.Invalidate();
            }
            catch
            {
                lblDownload.Text = "Download: Falha na Leitura";
                lblUpload.Text = "Upload: Falha na Leitura";
            }
        }

        private void PicGraph_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);

            // Draw Grid Lines
            Pen gridPen = new Pen(Color.FromArgb(40, 40, 40));
            for (int i = 0; i < picGraph.Width; i += 20)
                g.DrawLine(gridPen, i, 0, i, picGraph.Height);
            for (int i = 0; i < picGraph.Height; i += 20)
                g.DrawLine(gridPen, 0, i, picGraph.Width, i);

            // Calculate Scale Max
            int maxVal = 100;
            foreach (int val in speedHistory)
                if (val > maxVal) maxVal = val;

            Pen linePen = new Pen(Color.Cyan, 2);
            Point[] points = new Point[speedHistory.Count];

            float xStep = (float)picGraph.Width / (MaxGraphPoints - 1);

            for (int i = 0; i < speedHistory.Count; i++)
            {
                float y = picGraph.Height - ((float)speedHistory[i] / maxVal * (picGraph.Height - 10));
                points[i] = new Point((int)(i * xStep), (int)y);
            }

            if (points.Length > 1)
                g.DrawLines(linePen, points);

            // Draw Top Scale Label
            g.DrawString("Escala Máx: " + maxVal + " KB/s", new Font("Segoe UI", 7), Brushes.Yellow, new PointF(5, 5));
        }

        private void ActionPingRouter()
        {
            ActionPingCustom(routerIp);
        }

        private void ActionPingCustom(string host)
        {
            Log("Disparando pacote ICMP Echo para " + host + "...");

            Thread t = new Thread(new ThreadStart(() =>
            {
                try
                {
                    Ping ping = new Ping();
                    PingReply reply = ping.Send(host, 2000);

                    if (reply.Status == IPStatus.Success)
                    {
                        Log("Sucesso! Resposta de " + host + " | Tempo: " + reply.RoundtripTime + "ms | TTL: " + reply.Options.Ttl);
                    }
                    else
                    {
                        Log("Falha ao alcançar " + host + " | Status: " + reply.Status);
                    }
                }
                catch (Exception ex)
                {
                    Log("Erro no Ping: " + ex.Message);
                }
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void ActionScanServices()
        {
            btnScanPorts.Enabled = false;
            Log("Verificando portas de serviços padrão no host local...");

            Thread t = new Thread(new ThreadStart(() =>
            {
                int[] portsToTest = new int[] { 21, 22, 80, 135, 443, 445, 3389 };

                foreach (int port in portsToTest)
                {
                    bool isOpen = TestPort("127.0.0.1", port);
                    if (isOpen)
                        Log("Porta Local " + port + ": ABERTA / Escutando");
                    else
                        Log("Porta Local " + port + ": FECHADA / Sem Resposta");
                }

                Log("Varredura de serviços finalizada.");

                this.Invoke((MethodInvoker)delegate
                {
                    btnScanPorts.Enabled = true;
                });
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void ActionTraceroute(string target)
        {
            Log("Iniciando Rastreamento de Rota (Traceroute) para: " + target + "...");

            Thread t = new Thread(new ThreadStart(() =>
            {
                const int maxHops = 15;
                Ping pingSender = new Ping();
                PingOptions options = new PingOptions(1, true);
                Stopwatch timer = new Stopwatch();

                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    options.Ttl = ttl;
                    timer.Reset();
                    timer.Start();

                    try
                    {
                        PingReply reply = pingSender.Send(target, 3000, new byte[32], options);
                        timer.Stop();

                        if (reply.Status == IPStatus.Success)
                        {
                            Log(ttl + " | Resposta Final: " + reply.Address.ToString() + " em " + timer.ElapsedMilliseconds + "ms");
                            break;
                        }
                        else if (reply.Status == IPStatus.TtlExpired)
                        {
                            Log(ttl + " | Hop: " + reply.Address.ToString() + " em " + timer.ElapsedMilliseconds + "ms");
                        }
                        else
                        {
                            Log(ttl + " | * (Esgotado tempo de resposta)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ttl + " | Erro de rota: " + ex.Message);
                    }
                }
                Log("Traceroute concluído.");
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void ActionFlushDns()
        {
            Log("Executando limpeza de cache DNS do sistema...");

            Thread t = new Thread(new ThreadStart(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ipconfig /flushdns");
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;

                    Process p = Process.Start(psi);
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    Log("Resultado do Sistema: Cache do DNS resolvido com sucesso.");
                }
                catch (Exception ex)
                {
                    Log("Erro ao limpar DNS: " + ex.Message);
                }
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void LoadArpTable()
        {
            lstArp.Items.Clear();
            Thread t = new Thread(new ThreadStart(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("arp", "-a");
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;

                    Process p = Process.Start(psi);
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    this.Invoke((MethodInvoker)delegate
                    {
                        foreach (string line in lines)
                        {
                            string trimmed = line.Trim();
                            if (trimmed.StartsWith("Internet") || trimmed.StartsWith("Interface")) continue;

                            string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                ListViewItem item = new ListViewItem(parts[0]);
                                item.SubItems.Add(parts[1]);
                                item.SubItems.Add(parts[2]);
                                lstArp.Items.Add(item);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log("Erro ao ler tabela ARP: " + ex.Message);
                }
            }));

            t.IsBackground = true;
            t.Start();
        }

        private bool TestPort(string host, int port)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect(host, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(400, false);
                    return success && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}