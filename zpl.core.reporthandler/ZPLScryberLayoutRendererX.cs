using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using gip.core.autocomponent;
using gip.core.datamodel;
using gip.core.reporthandler;
using Scryber;
using Scryber.Components;
using Scryber.PDF;
using Scryber.PDF.Layout;
using ScryberDocument = Scryber.Components.Document;

namespace zpl.core.reporthandler
{
    /// <summary>
    /// First-pass Scryber layout renderer for ZPL output.
    /// Converts layout lines into a simple text label with sequential Y positioning.
    /// </summary>
    public sealed class ZPLScryberLayoutRendererX : IDocumentLayoutRenderer
    {
        public void Render(ScryberDocument document, PDFLayoutDocument layout, PDFLayoutContext layoutContext, Stream output)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            StringBuilder zpl = new StringBuilder();
            zpl.Append("^XA");

            for (int i = 0; i < layout.AllPages.Count; i++)
            {
                PDFLayoutPage page = layout.AllPages[i];
                if (page == null)
                    continue;

                if (i > 0)
                    zpl.Append("^XZ^XA");

                int y = 20;
                WriteBlock(zpl, document, page.HeaderBlock, ref y);
                WriteBlock(zpl, document, page.ContentBlock, ref y);
                WriteBlock(zpl, document, page.FooterBlock, ref y);
            }

            zpl.Append("^XZ");
            byte[] bytes = Encoding.ASCII.GetBytes(zpl.ToString());
            output.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBlock(StringBuilder zpl, ScryberDocument document, PDFLayoutBlock block, ref int y)
        {
            if (block == null)
                return;

            if (block.Columns != null)
            {
                foreach (PDFLayoutRegion column in block.Columns)
                    WriteRegion(zpl, document, column, ref y);
            }

            if (block.HasPositionedRegions && block.PositionedRegions != null)
            {
                foreach (PDFLayoutRegion positioned in block.PositionedRegions)
                    WriteRegion(zpl, document, positioned, ref y);
            }
        }

        private static void WriteRegion(StringBuilder zpl, ScryberDocument document, PDFLayoutRegion region, ref int y)
        {
            if (region == null || region.Contents == null)
                return;

            foreach (PDFLayoutItem item in region.Contents)
            {
                if (item is PDFLayoutLine line)
                {
                    WriteLine(zpl, document, line, ref y);
                }
                else if (item is PDFLayoutBlock block)
                {
                    WriteBlock(zpl, document, block, ref y);
                }
            }
        }

        private static void WriteLine(StringBuilder zpl, ScryberDocument document, PDFLayoutLine line, ref int y)
        {
            if (line?.Runs == null || line.Runs.Count == 0)
                return;

            int lineX = Math.Max(10, (int)Math.Round(line.OffsetX.PointsValue));
            int lineY = y;
            if (line.OffsetY.PointsValue > 0)
                lineY = Math.Max(lineY, (int)Math.Round(line.OffsetY.PointsValue));

            StringBuilder text = new StringBuilder();
            int consumedHeight = 40;

            foreach (PDFLayoutRun run in line.Runs)
            {
                if (TryAppendBarcodeRun(zpl, document, run, lineX, lineY, out int barcodeHeight))
                {
                    if (barcodeHeight > consumedHeight)
                        consumedHeight = barcodeHeight;
                    continue;
                }

                string runText = ExtractRunText(run);
                if (!string.IsNullOrEmpty(runText))
                    text.Append(runText);
            }

            string plainText = text.ToString().TrimEnd();
            if (!string.IsNullOrWhiteSpace(plainText))
            {
                zpl.Append("^FO");
                zpl.Append(lineX);
                zpl.Append(",");
                zpl.Append(lineY);
                zpl.Append("^A0N,30,24^FD");
                zpl.Append(EscapeZplData(plainText));
                zpl.Append("^FS");
            }

            y = lineY + consumedHeight;
        }

        private static bool TryAppendBarcodeRun(StringBuilder zpl, ScryberDocument document, PDFLayoutRun run, int defaultX, int y, out int consumedHeight)
        {
            consumedHeight = 40;

            Component component = run?.Owner as Component;
            if (!TryGetMetadata(component, out string barcodeTypeValue, "barcode-type", "zpl-barcode-type"))
                return false;

            string barcodeValue = ResolveBarcodeValue(document, component, run, out GS1Model gs1Model);
            if (string.IsNullOrWhiteSpace(barcodeValue))
                return true;

            int x = GetIntMetadata(component, defaultX, "barcode-x");

            if (barcodeTypeValue.Equals("QRCODE", StringComparison.OrdinalIgnoreCase))
            {
                int qrScale = GetIntMetadata(component, 4, "barcode-height", "qr-pixels-per-module", "barcode-width");
                if (qrScale > 10)
                    qrScale = 10;
                else if (qrScale < 1)
                    qrScale = 1;

                zpl.Append("^FO");
                zpl.Append(x);
                zpl.Append(",");
                zpl.Append(y);
                zpl.Append("^BQN,2,");
                zpl.Append(qrScale);
                zpl.Append("^FDLA,");
                zpl.Append(EscapeZplData(barcodeValue));
                zpl.Append("^FS");

                consumedHeight = qrScale * 34;
                if (consumedHeight < 40)
                    consumedHeight = 40;
                return true;
            }

            int barcodeHeight = GetIntMetadata(component, 100, "barcode-height");
            if (barcodeHeight < 20)
                barcodeHeight = 20;

            if (barcodeTypeValue.Equals("CODE128", StringComparison.OrdinalIgnoreCase) && gs1Model != null && gs1Model.IsGs1 && !string.IsNullOrWhiteSpace(gs1Model.ZplPayload))
            {
                zpl.Append("^FO");
                zpl.Append(x);
                zpl.Append(",");
                zpl.Append(y);
                zpl.Append("^BC,");
                zpl.Append(barcodeHeight);
                zpl.Append(",Y,N,Y,N^BY2,3^FD");
                zpl.Append(EscapeZplData(gs1Model.ZplPayload));
                zpl.Append("^FS");

                consumedHeight = barcodeHeight + 30;
                return true;
            }

            if (barcodeTypeValue.Equals("CODE128", StringComparison.OrdinalIgnoreCase))
            {
                zpl.Append("^FO");
                zpl.Append(x);
                zpl.Append(",");
                zpl.Append(y);
                zpl.Append("^BCN,");
                zpl.Append(barcodeHeight);
                zpl.Append(",Y,N,N^FD");
                zpl.Append(EscapeZplData(barcodeValue));
                zpl.Append("^FS");

                consumedHeight = barcodeHeight + 20;
                return true;
            }

            return false;
        }

        private static string ResolveBarcodeValue(ScryberDocument document, Component component, PDFLayoutRun run, out GS1Model gs1Model)
        {
            gs1Model = null;

            string value = ExtractRunText(run)?.Trim();
            if (TryGetMetadata(component, out string explicitValue, "barcode-value"))
                value = explicitValue;

            if (TryBuildGs1Model(document, component, out gs1Model) && !string.IsNullOrWhiteSpace(gs1Model.RawGs1Value))
                value = gs1Model.RawGs1Value;

            return value;
        }

        private static bool TryBuildGs1Model(ScryberDocument document, Component component, out GS1Model model)
        {
            model = null;
            if (document == null || component == null)
                return false;

            if (!TryGetMetadata(component, out string vbShowColumns, "vb-show-columns"))
                return false;
            if (!TryGetMetadata(component, out string vbShowColumnsKeys, "vb-show-columns-keys"))
                return false;
            if (!TryGetMetadata(component, out string vbContentPath, "vb-content"))
                return false;

            if (!document.Params.TryGetValue("reportData", out object rawReportData) || !(rawReportData is ReportData reportData))
                return false;

            object source = ScryberReportEngine.ResolveVBContent(reportData, vbContentPath);
            if (source == null)
                return false;

            string[] aiKeys = SplitCsv(vbShowColumnsKeys);
            string[] valueIdentifiers = SplitCsv(vbShowColumns);
            if (aiKeys.Length == 0 || aiKeys.Length != valueIdentifiers.Length)
                return false;

            List<(string ai, string val, bool variable)> input = GS1.GetGS1Data(source, aiKeys, valueIdentifiers);
            if (input == null || input.Count == 0)
                return false;

            model = GS1.GetGS1Model(aiKeys, input);
            return model != null && !string.IsNullOrWhiteSpace(model.RawGs1Value);
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();
        }

        private static string ExtractRunText(PDFLayoutRun run)
        {
            if (run is PDFTextRunCharacter full)
            {
                return full.Characters ?? string.Empty;
            }

            if (run is PDFTextRunPartialCharacter partial)
            {
                if (string.IsNullOrEmpty(partial.Characters))
                    return string.Empty;

                int start = Math.Max(0, partial.StartOffset);
                int count = Math.Max(0, partial.CharacterCount);
                if (start >= partial.Characters.Length || count == 0)
                    return string.Empty;

                if ((start + count) > partial.Characters.Length)
                    count = partial.Characters.Length - start;

                return partial.Characters.Substring(start, count);
            }

            return string.Empty;
        }

        private static bool TryGetMetadata(Component component, out string value, params string[] keys)
        {
            value = null;
            if (component == null || keys == null || keys.Length == 0)
                return false;

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (component.TryGetMetadata(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    value = value.Trim();
                    return true;
                }
            }

            return false;
        }

        private static int GetIntMetadata(Component component, int defaultValue, params string[] keys)
        {
            if (TryGetMetadata(component, out string raw, keys) && int.TryParse(raw, out int parsed))
                return parsed;

            return defaultValue;
        }

        private static string ExtractLineText(PDFLayoutLine line)
        {
            if (line?.Runs == null || line.Runs.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            foreach (PDFLayoutRun run in line.Runs)
            {
                if (run is PDFTextRunCharacter full)
                {
                    if (!string.IsNullOrEmpty(full.Characters))
                        builder.Append(full.Characters);
                }
                else if (run is PDFTextRunPartialCharacter partial)
                {
                    if (string.IsNullOrEmpty(partial.Characters))
                        continue;

                    int start = Math.Max(0, partial.StartOffset);
                    int count = Math.Max(0, partial.CharacterCount);
                    if (start >= partial.Characters.Length || count == 0)
                        continue;

                    if ((start + count) > partial.Characters.Length)
                        count = partial.Characters.Length - start;

                    builder.Append(partial.Characters.Substring(start, count));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string EscapeZplData(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("^", " ")
                .Replace("~", " ")
                .Replace("\r", string.Empty)
                .Replace("\n", " ");
        }
    }
}
