using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;

namespace DotAAnalyzer
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>


        

        

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(DotaAnalyzerAutosyncForm.Instance);
        }

    }
}















/*

           Bitmap screenshot = Screenshot();

           ImageViewer viewer = new ImageViewer(); //create an image viewer
           Image<Bgr, Byte> myImage = new Image<Bgr, Byte>(screenshot);
           Application.Idle += new EventHandler(delegate (object sender, EventArgs e)
           {  //run this until application closed (close button click on image viewer)
               viewer.Image = myImage; //draw the image obtained from camera
           });
           viewer.ShowDialog(); //show the image viewer
           CvInvoke.WaitKey(0);














           Mat img = CvInvoke.Imread("C:\\Users\\Daniel\\source\\repos\\DotAAnalyzer\\DotAAnalyzer\\tinker.png", Emgu.CV.CvEnum.ImreadModes.AnyColor);
           String win1 = "Test Window"; //The name of the window
           CvInvoke.NamedWindow(win1); //Create the window using the specific name



           Image<Bgr, Byte> templateImage = img.ToImage<Bgr, Byte>();
           Image<Gray, float> result = myImage.MatchTemplate(templateImage, TemplateMatchingType.SqdiffNormed);


           double min = 0, max = 0;
           Point maxp = new Point(0, 0);
           Point minp = new Point(0, 0);
           CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

           if (min < 0.01)
               Console.WriteLine("Found");
           else
               Console.WriteLine("NOT FOUND");

           //CvInvoke.Imshow(win1, img); //Show the image
           CvInvoke.WaitKey(0);  //Wait for the key pressing event

           //CvInvoke.DestroyWindow(win1); //Destroy the window if key is pressed
           */




/*
String win1 = "Test Window"; //The name of the window
CvInvoke.NamedWindow(win1); //Create the window using the specific name

Mat img = new Mat(200, 400, DepthType.Cv8U, 3); //Create a 3 channel image of 400x200
img.SetTo(new Bgr(255, 0, 0).MCvScalar); // set it to Blue color

//Draw "Hello, world." on the image using the specific font
CvInvoke.PutText(
   img,
   "Hello, world",
   new System.Drawing.Point(10, 80),
   FontFace.HersheyComplex,
   1.0,
   new Bgr(0, 255, 0).MCvScalar);


CvInvoke.Imshow(win1, img); //Show the image
*/
