using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using System.Collections.Generic;

namespace zpl.core.reporthandlerwpf
{
    public class ZplGs1Code128 : ZplPositionedElementBase
    {
        private readonly string _fdData;
        private readonly int _height;
        private readonly int _moduleWidth;
        private readonly int _ratio;
        private readonly bool _printHri;
        private readonly bool _hriAbove;

        public ZplGs1Code128(
            int x, int y,
            string fdData,
            int height,
            int moduleWidth = 2,
            int ratio = 3,
            bool printHri = true,
            bool hriAbove = false
        ) : base(x, y)
        {
            _fdData = fdData ?? "";
            _height = height > 0 ? height : 100;
            _moduleWidth = moduleWidth;
            _ratio = ratio;
            _printHri = printHri;
            _hriAbove = hriAbove;
        }

        public override IEnumerable<string> Render(ZplRenderOptions context)
        {
            yield return $"^FO{PositionX},{PositionY}";
            yield return $"^BC,{_height},{(_printHri ? "Y" : "N")},{(_hriAbove ? "Y" : "N")},Y,N";
            yield return $"^BY{_moduleWidth},{_ratio}";
            yield return $"^FD{_fdData}^FS";
        }
    }
}
