using BinaryKits.Zpl.Label;
using gip.core.reporthandler;
using System;
using System.Text;

namespace zpl.core.reporthandler
{
    public sealed class ZPLPrinterXShared
    {
        public string BuildCommands(PrintJob printJob, Encoding fallbackEncoding, short printDpi, int labelHeight)
        {
            string commands = BuildCommandsInternal(printJob, fallbackEncoding, printDpi);
            if (string.IsNullOrWhiteSpace(commands))
                return string.Empty;

            return EnsureLabelLength(commands, labelHeight);
        }

        private static string BuildCommandsInternal(PrintJob printJob, Encoding fallbackEncoding, short printDpi)
        {
            if (printJob is IZPLPrintJob zplPrintJob && zplPrintJob.ZplElements != null && zplPrintJob.ZplElements.Count > 0)
            {
                ZplRenderOptions renderOptions = new ZplRenderOptions
                {
                    TargetPrintDpi = printDpi
                };

                ZplEngine zplEngine = new ZplEngine(zplPrintJob.ZplElements);
                return zplEngine.ToZplString(renderOptions);
            }

            if (printJob?.Main != null && printJob.Main.Length > 0)
            {
                Encoding encoding = printJob.Encoding ?? fallbackEncoding ?? Encoding.ASCII;
                return encoding.GetString(printJob.Main);
            }

            return string.Empty;
        }

        private static string EnsureLabelLength(string commands, int labelHeight)
        {
            if (string.IsNullOrWhiteSpace(commands) || labelHeight <= 0)
                return commands;

            const string startLabel = "^XA";
            int startPos = commands.IndexOf(startLabel, StringComparison.Ordinal);
            if (startPos < 0)
                return commands;

            string labelLengthCmd = $"^LL{labelHeight}";
            int insertPos = startPos + startLabel.Length;
            int existingLabelPos = commands.IndexOf("^LL", insertPos, StringComparison.OrdinalIgnoreCase);
            if (existingLabelPos == insertPos)
                return commands;

            return commands.Insert(insertPos, labelLengthCmd);
        }
    }
}