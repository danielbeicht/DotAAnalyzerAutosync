using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotAAnalyzer
{
    class Hero
    {
        public int id;
        public string name;
        public Image<Bgr, Byte> image;
        public bool alreadyDetected;



        public Hero(int id, string name)
        {
            this.id = id;
            this.name = name;



            try
            { 
                //Mat templateMat = CvInvoke.Imread("C:\\Users\\Daniel\\source\\repos\\DotAAnalyzer\\DotAAnalyzer\\img\\" + name + ".png", Emgu.CV.CvEnum.ImreadModes.AnyColor);
                //Mat templateMat = CvInvoke.Imread(DotAAnalyzer.Properties.Resources.ResourceManager.GetObject(name), Emgu.CV.CvEnum.ImreadModes.AnyColor);

                object o = DotAAnalyzer.Properties.Resources.ResourceManager.GetObject(name);
                //System.Drawing.Image i = (System.Drawing.Image)o;
                //Bitmap m = (Bitmap)i;
                //object bmp = DotAAnalyzer.Properties.Resources.ResourceManager.GetObject(name);


                //Mat templateMat = CvInvoke.Imread("images\\" + name + ".png", Emgu.CV.CvEnum.ImreadModes.AnyColor);
                //Mat templateMat = CvInvoke.Imread("Bane.jpg", Emgu.CV.CvEnum.ImreadModes.AnyColor);
                //image = templateMat.ToImage<Bgr, Byte>();
                //image = bmp.ToImage<Bgr, Byte>();

                image = new Image<Bgr, Byte>((Bitmap) o);
            }
            catch
            {
                Console.WriteLine("Couldnt loadd " + name);
            }


        }



    }
}
