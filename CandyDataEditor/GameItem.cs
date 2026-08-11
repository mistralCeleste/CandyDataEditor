using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CandyDataEditor
{
    // GameItem.cs
    public class GameItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DescriptionHtml { get; set; } = string.Empty; // TipTap Output
    }
}
