using BinaryKits.Zpl.Label;
using gip.core.reporthandler;
using System;
using System.Text;

namespace zpl.core.reporthandler
{
    public sealed class ZPLPrinterXShared
    {
        public string BuildCommands(PrintJob printJob, Encoding fallbackEncoding, short printDpi, int labelHeight, string printerConfiguration = null)
        {
            string contentCommand = BuildCommandsInternal(printJob, fallbackEncoding, printDpi);
            if (string.IsNullOrWhiteSpace(contentCommand))
                return string.Empty;

            contentCommand = EnsureLabelLength(contentCommand, labelHeight);

            if (!string.IsNullOrWhiteSpace(printerConfiguration))
            {
                string configurationCommand = EnsureLabelLengthInConfiguration(printerConfiguration, labelHeight);            
                if (!string.IsNullOrWhiteSpace(configurationCommand))
                    contentCommand = configurationCommand + Environment.NewLine + contentCommand;
            }

            return contentCommand;
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

        private static string EnsureLabelLengthInConfiguration(string commands, int labelHeight)
        {
            if (string.IsNullOrWhiteSpace(commands) || labelHeight <= 0)
                return commands;

            const string placeholder = "! U1 setvar \"zpl.label_length\" \"{labelHeight}\"";

            if (!commands.Contains(placeholder))
                return commands;

            string replacement = $"! U1 setvar \"zpl.label_length\" \"{labelHeight}\"";
            return commands.Replace(placeholder, replacement);
        }
    }
}