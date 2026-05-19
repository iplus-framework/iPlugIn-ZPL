using gip.core.autocomponent;
using gip.core.datamodel;
using gip.core.reporthandler;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace zpl.core.reporthandler
{
    [ACClassInfo(Const.PackName_VarioSystem, "en{'ZPLPrinterX'}de{'ZPLPrinterX'}", Global.ACKinds.TPABGModule, Global.ACStorableTypes.Required, false, false)]
    public class ZPLPrinterX : ACPrintServerBase
    {
        private readonly ZPLPrinterXShared _shared;
        private ACPropertyConfigValue<short> _PrintDPI;
        private ACPropertyConfigValue<bool> _UseScryberLayoutRenderer;
        private ACPropertyConfigValue<int> _LabelHeight;
        private ACPropertyConfigValue<double> _LabelHeightMM;

        public ZPLPrinterX(ACClass acType, IACObject content, IACObject parentACObject, ACValueList parameter, string acIdentifier = "")
            : base(acType, content, parentACObject, parameter, acIdentifier)
        {
            _shared = new ZPLPrinterXShared();
            _UseScryberLayoutRenderer = new ACPropertyConfigValue<bool>(this, nameof(UseScryberLayoutRenderer), true);
            _PrintDPI = new ACPropertyConfigValue<short>(this, nameof(PrintDPI), 203);
            _LabelHeight = new ACPropertyConfigValue<int>(this, nameof(LabelHeight), 800);
            _LabelHeightMM = new ACPropertyConfigValue<double>(this, nameof(LabelHeightMM), 0);
        }

        public override bool ACInit(Global.ACStartTypes startChildMode = Global.ACStartTypes.Automatic)
        {
            bool init = base.ACInit(startChildMode);
            _ = UseScryberLayoutRenderer;
            _ = PrintDPI;
            return init;
        }

        [ACPropertyConfig("en{'Use Scryber layout renderer'}de{'Scryber-Layout-Renderer verwenden'}")]
        public bool UseScryberLayoutRenderer
        {
            get => _UseScryberLayoutRenderer.ValueT;
            set => _UseScryberLayoutRenderer.ValueT = value;
        }

        [ACPropertyConfig("en{'Print DPI'}de{'Print DPI'}")]
        public short PrintDPI
        {
            get => _PrintDPI.ValueT;
            set => _PrintDPI.ValueT = value;
        }

        [ACPropertyConfig("en{'Label Height (dots)'}de{'Label Höhe (Punkte)'}")]
        public int LabelHeight
        {
            get => _LabelHeight.ValueT;
            set => _LabelHeight.ValueT = value;
        }

        [ACPropertyConfig("en{'Label Height (mm) - if > 0, overrides dots setting'}de{'Label Höhe (mm) - wenn > 0, überschreibt Punkte-Einstellung'}")]
        public double LabelHeightMM
        {
            get => _LabelHeightMM.ValueT;
            set => _LabelHeightMM.ValueT = value;
        }

        public int EffectiveLabelHeight
        {
            get
            {
                if (LabelHeightMM > 0)
                {
                    double inches = LabelHeightMM / 25.4;
                    return (int)Math.Round(inches * PrintDPI);
                }

                return LabelHeight;
            }
        }

        public string _LastPrintCommand;
        [ACPropertyInfo(9999)]
        public string LastPrintCommand
        {
            get => _LastPrintCommand;
            set
            {
                _LastPrintCommand = value;
                OnPropertyChanged();
            }
        }

        [ACPropertyBindingSource(9999, "Error", "en{'ZPL printer alarm'}de{'ZPL Drucker Alarm'}", "", false, false)]
        public IACContainerTNet<PANotifyState> ZPLPrinterAlarm { get; set; }

        [ACPropertyBindingSource(9999, "Error", "en{'Printer configuration'}de{'Drucker Konfiguration'}", "", false, true)]
        public IACContainerTNet<string> ZPLPrinterConfiguration { get; set; }

        protected override PrintJob TryCreateScryberCustomPrintJob(ACClassDesign aCClassDesign, ReportData reportData)
        {
            if (!UseScryberLayoutRenderer || aCClassDesign == null || reportData == null)
                return null;

            string template = GetScryberTemplate(aCClassDesign);
            if (string.IsNullOrWhiteSpace(template))
                return null;

            try
            {
                Encoding encoding = ResolveEncoding();
                ZPLScryberLayoutRendererX renderer = new ZPLScryberLayoutRendererX();
                _ = ScryberReportEngine.RenderWithLayoutRenderer(template, reportData, renderer);
                if (renderer.ZplElements == null || renderer.ZplElements.Count == 0)
                    return null;

                ZPLPrintJobX zplPrintJob = new ZPLPrintJobX
                {
                    Name = aCClassDesign.ACIdentifier,
                    Encoding = encoding,
                    ColumnMultiplier = 1,
                    ColumnDivisor = 1,
                };

                foreach (var element in renderer.ZplElements)
                {
                    if (element != null)
                        zplPrintJob.ZplElements.Add(element);
                }

                return zplPrintJob;
            }
            catch (Exception ex)
            {
                Messages.LogException(GetACUrl(), nameof(TryCreateScryberCustomPrintJob), ex);
                return null;
            }
        }

        private Encoding ResolveEncoding()
        {
            Encoding encoder = Encoding.ASCII;
            if (CodePage <= 0)
                return encoder;

            try
            {
                return Encoding.GetEncoding(CodePage);
            }
            catch (Exception ex)
            {
                Messages.LogException(GetACUrl(), nameof(ResolveEncoding), ex);
                return encoder;
            }
        }

        public override bool SendDataToPrinter(PrintJob printJob)
        {
            if (printJob == null)
                return false;

            for (int tries = 0; tries < PrintTries; tries++)
            {
                try
                {
                    string commands = _shared.BuildCommands(printJob, ResolveEncoding(), PrintDPI, EffectiveLabelHeight);

                    if (string.IsNullOrEmpty(commands))
                    {
                        string message = "Print command is empty!";
                        if (IsAlarmActive(ZPLPrinterAlarm, message) == null)
                            Messages.LogError(GetACUrl(), nameof(SendDataToPrinter), message);
                        OnNewAlarmOccurred(ZPLPrinterAlarm, message);
                        return false;
                    }

                    LastPrintCommand = commands;
                    SendData(commands);
                    return true;
                }
                catch (Exception e)
                {
                    string message = string.Format("Print failed on {0}. See log for further details.", IPAddress);
                    if (IsAlarmActive(ZPLPrinterAlarm, message) == null)
                        Messages.LogException(GetACUrl(), nameof(SendDataToPrinter), e);
                    OnNewAlarmOccurred(ZPLPrinterAlarm, message);
                    IsConnected.ValueT = false;
                    Thread.Sleep(5000);
                }
            }

            return false;
        }

        public override void SendDataBeforePrint(PrintJob printJob)
        {
            if (ZPLPrinterConfiguration != null)
            {
                string printerConfiguration = ZPLPrinterConfiguration.ValueT;
                if (!string.IsNullOrEmpty(printerConfiguration))
                    SendData(printerConfiguration);
            }
        }

        public void SendData(string printCommand)
        {
            try
            {
                TcpClient tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress, Port);

                if (tcpClient.Connected)
                {
                    IsConnected.ValueT = true;

                    StreamWriter writer = new StreamWriter(tcpClient.GetStream());
                    writer.Write(printCommand);
                    writer.Flush();
                    writer.Close();
                }
                else
                {
                    IsConnected.ValueT = false;
                }

                tcpClient.Close();
            }
            catch (Exception e)
            {
                ZPLPrinterAlarm.ValueT = PANotifyState.AlarmOrFault;
                if (IsAlarmActive(nameof(ZPLPrinterAlarm), e.Message) == null)
                    Messages.LogException(GetACUrl(), $"{nameof(ZPLPrinterX)}.{nameof(SendData)}", e);

                OnNewAlarmOccurred(ZPLPrinterAlarm, e.Message, true);
            }
        }

        protected override PrintJob OnDoPrint(ACClassDesign aCClassDesign, int codePage, ReportData reportData)
        {
            return null;
        }
    }
}
