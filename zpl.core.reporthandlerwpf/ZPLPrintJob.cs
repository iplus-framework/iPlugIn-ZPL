using BinaryKits.Zpl.Label.Elements;
using gip.core.reporthandler;
using System.Collections.Generic;

namespace zpl.core.reporthandlerwpf
{
    public class ZPLPrintJob : PrintJob
    {
        public ZPLPrintJob() : base()
        {
            NextYPosition = 10;
        }

        private List<ZplPositionedElementBase> _ZplElements;
        public List<ZplPositionedElementBase> ZplElements
        {
            get
            {
                if (_ZplElements == null)
                    _ZplElements = new List<ZplPositionedElementBase>();

                return _ZplElements;
            }
        }

        public int NextYPosition
        {
            get;
            private set;
        }

        public void AddToJob(ZplPositionedElementBase element, int elementHeight)
        {
            if (element != null)
            {
                ZplElements.Add(element);
                NextYPosition = element.PositionY + elementHeight + 10;
            }
            else if (elementHeight > 0)
            {
                NextYPosition += elementHeight;
            }
        }
    }
}
