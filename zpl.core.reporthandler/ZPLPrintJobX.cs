using BinaryKits.Zpl.Label.Elements;
using gip.core.reporthandler;
using System.Collections.Generic;

namespace zpl.core.reporthandler
{
    public class ZPLPrintJobX : PrintJob, IZPLPrintJob
    {
        public ZPLPrintJobX() : base()
        {
            NextYPosition = 10;
        }

        private List<ZplPositionedElementBase> _zplElements;
        public List<ZplPositionedElementBase> ZplElements
        {
            get
            {
                if (_zplElements == null)
                    _zplElements = new List<ZplPositionedElementBase>();

                return _zplElements;
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