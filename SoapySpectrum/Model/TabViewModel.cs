using SoapySA.Extentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoapySA.Model
{
    public abstract class TabViewModel 
    {
        public abstract string tabName { get; }
        public abstract void Render();
    }
}
