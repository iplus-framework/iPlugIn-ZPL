﻿using gip.core.datamodel;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Documents;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using gip.core.autocomponent;
using System.Net.Sockets;
using System.IO;
using gip.core.reporthandlerwpf;
using gip.core.reporthandler;
using gip.core.reporthandlerwpf.Flowdoc;

namespace zpl.core.reporthandlerwpf
{
    [ACClassInfo(Const.PackName_VarioSystem, "en{'ZPLPrinter'}de{'ZPLPrinter'}", Global.ACKinds.TPABGModule, Global.ACStorableTypes.Required, false, false)]
    public class ZPLPrinter : ACPrintServerBaseWPF
    {
        #region c'tors

        public ZPLPrinter(ACClass acType, IACObject content, IACObject parentACObject, ACValueList parameter, string acIdentifier = "") : 
            base(acType, content, parentACObject, parameter, acIdentifier)
        {
            _PrintDPI = new ACPropertyConfigValue<short>(this, nameof(PrintDPI), 203);
        }

        #endregion

        #region Properties

        private Queue<ZPLPrintJob> _ZPLPrintJobs;
        public Queue<ZPLPrintJob> ZPLPrintJobs
        {
            get
            {
                if (_ZPLPrintJobs == null)
                    _ZPLPrintJobs = new Queue<ZPLPrintJob>();
                return _ZPLPrintJobs;
            }
        }

        private ACPropertyConfigValue<short> _PrintDPI;
        [ACPropertyConfig("en{'Print DPI'}de{'Print DPI'}")]
        public short PrintDPI
        {
            get => _PrintDPI.ValueT;
            set => _PrintDPI.ValueT = value;
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
        public IACContainerTNet<string> ZPLPrinterConfiguration
        {
            get;
            set;
        }


        #endregion

        #region Methods 

        #region Methods -> Render

        /// <summary>
        /// Convert report data to stream
        /// </summary>
        /// <param name="reportData"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override bool SendDataToPrinter(PrintJob printJob)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;

            if (zplPrintJob == null)
                return false;

            for (int tries = 0; tries < PrintTries; tries++)
            {
                try
                {
                    ZplRenderOptions renderOptions = new ZplRenderOptions();
                    renderOptions.TargetPrintDpi = PrintDPI;
                    ZplEngine zplEngine = new ZplEngine(zplPrintJob.ZplElements);
                    string commands = zplEngine.ToZplString(renderOptions);

                    if (string.IsNullOrEmpty(commands))
                    {
                        string message = "Print command is empty!";
                        if (IsAlarmActive(ZPLPrinterAlarm, message) == null)
                            Messages.LogError(GetACUrl(), "SendDataToPrinter(10)", message);
                        OnNewAlarmOccurred(ZPLPrinterAlarm, message);

                        return false;
                    }

                    LastPrintCommand = commands;
                    SendData(commands);

                    return true;
                }
                catch (Exception e)
                {
                    string message = String.Format("Print failed on {0}. See log for further details.", IPAddress);
                    if (IsAlarmActive(ZPLPrinterAlarm, message) == null)
                        Messages.LogException(GetACUrl(), "SendDataToPrinter(20)", e);
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
                {
                    SendData(printerConfiguration);
                }
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

                    // Send Zpl data to printer
                    StreamWriter writer = new System.IO.StreamWriter(tcpClient.GetStream());
                    writer.Write(printCommand);
                    writer.Flush();

                    // Close Connection
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
                    Messages.LogException(GetACUrl(), $"{nameof(ZPLPrinter)}.{nameof(SendData)}(10)", e);

                OnNewAlarmOccurred(ZPLPrinterAlarm, e.Message, true);
            }
        }

        public override PrintJob GetPrintJob(string reportName, FlowDocument flowDocument)
        {
            ZPLPrintJob printJob = new ZPLPrintJob();
            printJob.FlowDocument = flowDocument;
            printJob.ColumnMultiplier = 1;
            printJob.ColumnDivisor = 1;
            OnRenderFlowDocument(printJob, printJob.FlowDocument);
            return printJob;
        }

        #region Methods -> Render -> Block

        public override void OnRenderBlockHeader(PrintJob printJob, Block block, BlockDocumentPosition position)
        {
        }

        public override void OnRenderBlockFooter(PrintJob printJob, Block block, BlockDocumentPosition position)
        {
        }

        public override void OnRenderSectionReportHeaderHeader(PrintJob printJob, SectionReportHeader sectionReportHeader)
        {
        }

        public override void OnRenderSectionReportHeaderFooter(PrintJob printJob, SectionReportHeader sectionReportHeader)
        {
        }

        public override void OnRenderSectionReportFooterHeader(PrintJob printJob, SectionReportFooter sectionReportFooter)
        {
        }

        public override void OnRenderSectionReportFooterFooter(PrintJob printJob, SectionReportFooter sectionReportFooter)
        {
        }

        public override void OnRenderSectionDataGroupHeader(PrintJob printJob, SectionDataGroup sectionDataGroup)
        {
        }

        public override void OnRenderSectionDataGroupFooter(PrintJob printJob, SectionDataGroup sectionDataGroup)
        {
        }

        #endregion

        #region Methods -> Render -> Table


        public override void OnRenderSectionTableHeader(PrintJob printJob, Table table)
        {
        }

        public override void OnRenderSectionTableFooter(PrintJob printJob, Table table)
        {
        }


        public override void OnRenderTableColumn(PrintJob printJob, TableColumn tableColumn)
        {
        }

        public override void OnRenderTableRowGroupHeader(PrintJob printJob, TableRowGroup tableRowGroup)
        {
        }

        public override void OnRenderTableRowGroupFooter(PrintJob printJob, TableRowGroup tableRowGroup)
        {
        }

        public override void OnRenderTableRowHeader(PrintJob printJob, TableRow tableRow)
        {
        }

        public override void OnRenderTableRowFooter(PrintJob printJob, TableRow tableRow)
        {
        }

        #endregion

        #region Methods -> Render -> Inlines

        public override void OnRenderParagraphHeader(PrintJob printJob, Paragraph paragraph)
        {
        }

        public override void OnRenderParagraphFooter(PrintJob printJob, Paragraph paragraph)
        {
        }

        public override void OnRenderInlineContextValue(PrintJob printJob, InlineContextValue inlineContextValue)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                ZplFont font = new ZplFont(inlineContextValue.FontWidth, (int)inlineContextValue.FontSize);
                (int posX, int posY) = GetInlinePos(inlineContextValue, zplPrintJob);
                ZplTextField textField = new ZplTextField(inlineContextValue.Text, posX, posY, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderInlineDocumentValue(PrintJob printJob, InlineDocumentValue inlineDocumentValue)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                ZplFont font = new ZplFont(inlineDocumentValue.FontWidth, (int)inlineDocumentValue.FontSize);
                (int posX, int posY) = GetInlinePos(inlineDocumentValue, zplPrintJob);
                ZplTextField textField = new ZplTextField(inlineDocumentValue.Text, posX, posY, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderInlineACMethodValue(PrintJob printJob, InlineACMethodValue inlineACMethodValue)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                ZplFont font = new ZplFont(inlineACMethodValue.FontWidth, (int)inlineACMethodValue.FontSize);
                (int posX, int posY) = GetInlinePos(inlineACMethodValue, zplPrintJob);
                ZplTextField textField = new ZplTextField(inlineACMethodValue.Text, posX, posY, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderInlineTableCellValue(PrintJob printJob, InlineTableCellValue inlineTableCellValue)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                ZplFont font = new ZplFont(inlineTableCellValue.FontWidth, (int)inlineTableCellValue.FontSize);
                (int posX, int posY) = GetInlinePos(inlineTableCellValue, zplPrintJob);
                ZplTextField textField = new ZplTextField(inlineTableCellValue.Text, posX, posY, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderInlineBarcode(PrintJob printJob, InlineBarcode inlineBarcode)
        {
            if (printJob == null || inlineBarcode == null || inlineBarcode.Value == null)
                return;

            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob == null)
                return;

            string barcodeValue = inlineBarcode.Value.ToString();
            (int posX, int posY) = GetInlineUIPos(inlineBarcode, zplPrintJob);

            if (inlineBarcode.BarcodeType == BarcodeType.QRCODE)
            {
                int qrCodeHeight = inlineBarcode.BarcodeHeight;
                if (qrCodeHeight > 10)
                    qrCodeHeight = 10;
                else if (qrCodeHeight <= 1)
                    qrCodeHeight = 1;
                
                ZplQrCode qrCode = new ZplQrCode(barcodeValue, posX, posY, 2, qrCodeHeight);
                zplPrintJob.AddToJob(qrCode, qrCodeHeight * 34);
            }
            else
            {
                ZplBarcode128 barcode = new ZplBarcode128(barcodeValue, posX, posY, inlineBarcode.BarcodeHeight);
                zplPrintJob.AddToJob(barcode, inlineBarcode.BarcodeHeight);
            }
        }

        public override void OnRenderInlineBoolValue(PrintJob printJob, InlineBoolValue inlineBoolValue)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                ZplFont font = new ZplFont(inlineBoolValue.FontWidth, (int)inlineBoolValue.FontSize, "0");
                (int posX, int posY) = GetInlineUIPos(inlineBoolValue, zplPrintJob);
                ZplTextField textField = new ZplTextField(inlineBoolValue.Value.ToString(), posX, posY, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderRun(PrintJob printJob, Run run)
        {
            Paragraph parentParagraph = run.Parent as Paragraph;
            int leftPos = 10;
            int? topPos = null; 
            if (parentParagraph != null)
            {
                if (parentParagraph.Padding.Left > 0)
                    leftPos = (int)parentParagraph.Padding.Left;

                if (parentParagraph.Padding.Top > 0)
                    topPos = (int)parentParagraph.Padding.Top;
            }

            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                int yPos = zplPrintJob.NextYPosition;
                if (topPos.HasValue)
                    yPos = topPos.Value;

                ZplFont font = new ZplFont(0, (int)run.FontSize);
                ZplTextField textField = new ZplTextField(run.Text, leftPos, yPos, font);
                zplPrintJob.AddToJob(textField, font.FontHeight);
            }
        }

        public override void OnRenderLineBreak(PrintJob printJob, LineBreak lineBreak)
        {
            ZPLPrintJob zplPrintJob = printJob as ZPLPrintJob;
            if (zplPrintJob != null)
            {
                zplPrintJob.AddToJob(null, (int)lineBreak.FontSize);
            }
        }

        #endregion

        public (int,int) GetInlinePos(InlinePropertyValueBase inlineValue, ZPLPrintJob zplPrintJob)
        {
            int xPos = inlineValue.XPos > 0 ? inlineValue.XPos : 10;
            int yPos = inlineValue.YPos > 0 ? inlineValue.YPos : zplPrintJob.NextYPosition;

            return (xPos, yPos);
        }

        public (int, int) GetInlineUIPos(InlineUIValueBase inlineValue, ZPLPrintJob zplPrintJob)
        {
            int xPos = inlineValue.XPos > 0 ? inlineValue.XPos : 10;
            int yPos = inlineValue.YPos > 0 ? inlineValue.YPos : zplPrintJob.NextYPosition;

            return (xPos, yPos);
        }


        #endregion

        [ACMethodInteraction("", "en{'Switch to ZPL mode'}de{'Switch to ZPL mode'}", 500, true, ContextMenuCategoryIndex = (short)Global.ContextMenuCategory.Utilities)]
        public void SwitchPrinterToZPLMode()
        {
            string command = "! U1 setvar \"device.languages\" \"zpl\"" +
                             "! U1 setvar \"device.pnp_option\" \"zpl\"" +
                             "! U1 do \"device.reset\" \"\" <CR>";

            SendData(command);

        }

        [ACMethodInteraction("", "en{'Switch to CPCL mode'}de{'Switch to CPCL mode'}", 501, true, ContextMenuCategoryIndex = (short)Global.ContextMenuCategory.Utilities)]
        public void SwitchPrinterToCPCLMode()
        {
            string command = "! U1 setvar \"device.languages\" \"line_print\"" +
                             "! U1 setvar \"device.pnp_option\" \"cpcl\"" +
                             "! U1 do \"device.reset\" \"\" <CR>";

            SendData(command);
        }

        #endregion

        #region HandleExecuteHelpers

        protected override bool HandleExecuteACMethod(out object result, AsyncMethodInvocationMode invocationMode, string acMethodName, ACClassMethod acClassMethod, params object[] acParameter)
        {
            result = null;

            switch(acMethodName)
            {
                case nameof(SwitchPrinterToZPLMode):
                    SwitchPrinterToZPLMode();
                    return true;
                case nameof(SwitchPrinterToCPCLMode):
                    SwitchPrinterToCPCLMode();
                    return true;
            }

            return base.HandleExecuteACMethod(out result, invocationMode, acMethodName, acClassMethod, acParameter);
        }

        #endregion
    }
}
