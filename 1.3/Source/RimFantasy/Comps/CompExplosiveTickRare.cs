using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimFantasy
{
    public class CompExplosiveTickRare : CompExplosive
    {
        public override void CompTickRare()
        {
            base.CompTickRare();
            for (var i = 0; i < 250; i++)
            {
                base.CompTick();
            }
        }
    }
}
