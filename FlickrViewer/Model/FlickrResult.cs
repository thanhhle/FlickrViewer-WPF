using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlickrViewer.Model
{
    public class FlickrResult
    {
        public string Title { get; set; }
        public string URL { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }
}
