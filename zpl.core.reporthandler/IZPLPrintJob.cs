using BinaryKits.Zpl.Label.Elements;
using System.Collections.Generic;

namespace zpl.core.reporthandler
{
    public interface IZPLPrintJob
    {
        List<ZplPositionedElementBase> ZplElements { get; }

        int NextYPosition { get; }

        void AddToJob(ZplPositionedElementBase element, int elementHeight);
    }
}