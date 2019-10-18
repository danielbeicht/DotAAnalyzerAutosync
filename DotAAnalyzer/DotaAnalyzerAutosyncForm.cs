using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace DotAAnalyzer
{
    public partial class DotaAnalyzerAutosyncForm : Form
    {
        
        private static System.Timers.Timer aTimer;
        private static List<Hero> heroes;
        private static int _detectedHeroesCount = 0;
        private static string accountID;
        private static DotaGSIHandler gsiHandler;

        private static Bitmap lastScreenshot = null;

        private static Bitmap lastScreenshotRadiant1 = null;
        private static Bitmap lastScreenshotRadiant2 = null;
        private static Bitmap lastScreenshotRadiant3 = null;
        private static Bitmap lastScreenshotRadiant4 = null;
        private static Bitmap lastScreenshotRadiant5 = null;

        private static Bitmap lastScreenshotDire1 = null;
        private static Bitmap lastScreenshotDire2 = null;
        private static Bitmap lastScreenshotDire3 = null;
        private static Bitmap lastScreenshotDire4 = null;
        private static Bitmap lastScreenshotDire5 = null;


        private static DotaAnalyzerAutosyncForm instance = null;
        
        
        public DotaAnalyzerAutosyncForm()
        {
            InitializeComponent();
            
            InitializeHeroes();
            gsiHandler = DotaGSIHandler.Instance;

            aTimer = new System.Timers.Timer(2000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;

            lblStatus.Text = "Waiting for pick phase.";
        }
        
        
        public static DotaAnalyzerAutosyncForm Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DotaAnalyzerAutosyncForm();
                }
                return instance;
            }
        }


        
        
        private int detectedHeroesCount
        {
            get
            {
                return _detectedHeroesCount;
            }
            set
            {
                _detectedHeroesCount = value;
                if (_detectedHeroesCount == 10)
                {
                    StopTracking();
                    _detectedHeroesCount = 0;
                }
            }
        }

        public void StartTracking(string acID)
        {
            lblStatus.Invoke(new Action(() => lblStatus.Text = "Pick phase in progress. Tracking..."));
//            RadiantLabel1.Invoke(new Action(() => RadiantLabel1.Visible = true));
//            RadiantLabel2.Invoke(new Action(() => RadiantLabel2.Visible = true));
//            RadiantLabel3.Invoke(new Action(() => RadiantLabel3.Visible = true));
//            RadiantLabel4.Invoke(new Action(() => RadiantLabel4.Visible = true));
//            RadiantLabel5.Invoke(new Action(() => RadiantLabel5.Visible = true));
//            DireLabel1.Invoke(new Action(() => DireLabel1.Visible = true));
//            DireLabel2.Invoke(new Action(() => DireLabel2.Visible = true));
//            DireLabel3.Invoke(new Action(() => DireLabel3.Visible = true));
//            DireLabel4.Invoke(new Action(() => DireLabel4.Visible = true));
//            DireLabel5.Invoke(new Action(() => DireLabel5.Visible = true));
            
            
            

            accountID = acID;
            Console.WriteLine(accountID);

            foreach (Hero hero in heroes)
            {
                hero.alreadyDetected = false;
            }

            string json = "{\"accountID\":\"" + accountID + "\"" + "}";

            PostRequest("newMatch", json);


            aTimer.Enabled = true;
        }

        public void StopTracking()
        {

            if (aTimer.Enabled)
            {
                aTimer.Enabled = false;
                Thread.Sleep(1500);
                OnTimedEvent(null, null);
                lblStatus.Invoke(new Action(() => lblStatus.Text = "Waiting for pick phase."));
                ResetLabels();
            }
        }




        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}");

            Bitmap screenshotBitmap = Screenshot();
            Image<Bgr, Byte> screenshot = new Image<Bgr, Byte>(screenshotBitmap);


            //if (lastScreenshot != null)
            //{
            //    Image<Bgr, Byte> lScreen = new Image<Bgr, Byte>(lastScreenshot);
            //    Image<Gray, float> result = screenshot.MatchTemplate(lScreen, TemplateMatchingType.SqdiffNormed);

            //    double min = 0, max = 0;
            //    Point maxp = new Point(0, 0);
            //    Point minp = new Point(0, 0);
            //    CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

            //    if (min < 0.01)
            //    {
            //        Console.WriteLine("SAME");
            //    } else
            //    {
            //        Console.WriteLine("NOT SAME");
            //    }

            //    lastScreenshot = screenshotBitmap.Clone(new Rectangle(0, 0, screenshotBitmap.Width, screenshotBitmap.Height), screenshotBitmap.PixelFormat);
            //} else
            //{
            //    lastScreenshot = screenshotBitmap.Clone(new Rectangle(0, 0, screenshotBitmap.Width, screenshotBitmap.Height), screenshotBitmap.PixelFormat);
            //}



            List<Task> tasks = new List<Task>();

            foreach (Hero hero in heroes)
            {
                if (hero.image != null && !hero.alreadyDetected)
                {
                    Task task = Task.Factory.StartNew(() => DoWork(hero, screenshot));
                    tasks.Add(task);
                }
            }
            Task.WaitAll(tasks.ToArray());

            screenshot.Dispose();
        }


        private void DoWork(Hero hero, Image<Bgr, Byte> screenshot)
        {
            Image<Gray, float> result = screenshot.MatchTemplate(hero.image, TemplateMatchingType.SqdiffNormed);

            double min = 0, max = 0;
            Point maxp = new Point(0, 0);
            Point minp = new Point(0, 0);
            CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

            if (min < 0.01)
            {
                hero.alreadyDetected = true;
                detectedHeroesCount++;

                string isRadiant = "false";

                if (minp.X < 1280)
                {
                    isRadiant = "true";
                }

                string json = "{\"accountID\":\"" + accountID + "\"," +
                                  "\"heroID\":" + hero.id +
                                  ",\"isRadiant\":" + isRadiant + "}";

                PostRequest("addHeroToMatch", json);
                if (isRadiant.Equals("true"))
                {
                    RadiantPick(hero.name);
                }
                else
                {
                    DirePick(hero.name);
                }
            }

            result.Dispose();
        }



        private Bitmap Screenshot()
        {
            //Bitmap in größe der Bildschirmauflösung anlegen
            Bitmap screen = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Convert.ToInt32(Screen.PrimaryScreen.Bounds.Height * 0.07));

            //Graphics Objekt der Bitmap anlegen
            Graphics g = Graphics.FromImage(screen);

            //Bildschirminhalt auf die Bitmap zeichnen
            g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                     Convert.ToInt32(Screen.PrimaryScreen.Bounds.Y * 0.07),
                                     0, 0, screen.Size);
            g.Dispose();

            //Rectangle section = new Rectangle(new Point(278, 0), new Size(2002, 86));
            Rectangle section = new Rectangle(new Point(288, 0), new Size(135, 86));
            //screen = CropImage(screen, section);
            screen = CropImage(screen, section);












            if (lastScreenshot != null)
            {
                Image<Bgr, Byte> lScreen = new Image<Bgr, Byte>(lastScreenshot);
                Image<Bgr, Byte> cScreen = new Image<Bgr, Byte>(screen);
                Image<Gray, float> result = cScreen.MatchTemplate(lScreen, TemplateMatchingType.SqdiffNormed);

                double min = 0, max = 0;
                Point maxp = new Point(0, 0);
                Point minp = new Point(0, 0);
                CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

                if (min < 0.01)
                {
                    Console.WriteLine("SAME");
                }
                else
                {
                    Console.WriteLine("NOT SAME");
                }

                lastScreenshot = screen.Clone(new Rectangle(0, 0, screen.Width, screen.Height), screen.PixelFormat);
            }
            else
            {
                lastScreenshot = screen.Clone(new Rectangle(0, 0, screen.Width, screen.Height), screen.PixelFormat);
            }















































            //Screenshot zurückgeben
            pictureBox1.Invoke(new Action(() => pictureBox1.Image = screen.Clone(new Rectangle(0, 0, screen.Width, screen.Height), screen.PixelFormat)));
            pictureBox1.Invoke(new Action(() => pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize));


            return screen;
        }

        private Bitmap CropImage(Bitmap source, Rectangle section)
        {
            var bitmap = new Bitmap(section.Width, section.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);
                return bitmap;
            }
        }



        private void PostRequest(string requestType, string json)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://dota-analyzer.com/api/autosync/" + requestType);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";


            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(json);
            }

            Console.WriteLine("SEND MESSAGE");
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result1 = streamReader.ReadToEnd();
            }
        }


        private void InitializeHeroes()
        {
            heroes = new List<Hero>();
            heroes.Add(new Hero(1, "anti_mage"));
            heroes.Add(new Hero(2, "axe"));
            heroes.Add(new Hero(3, "bane"));
            heroes.Add(new Hero(4, "bloodseeker"));
            heroes.Add(new Hero(5, "crystal_maiden"));
            heroes.Add(new Hero(6, "drow_ranger"));
            heroes.Add(new Hero(7, "earthshaker"));
            heroes.Add(new Hero(8, "juggernaut"));
            heroes.Add(new Hero(9, "mirana"));
            heroes.Add(new Hero(10, "morphling"));
            heroes.Add(new Hero(11, "shadow_fiend"));
            heroes.Add(new Hero(12, "phantom_lancer"));
            heroes.Add(new Hero(13, "puck"));
            heroes.Add(new Hero(14, "pudge"));
            heroes.Add(new Hero(15, "razor"));
            heroes.Add(new Hero(16, "sand_king"));
            heroes.Add(new Hero(17, "storm_spirit"));
            heroes.Add(new Hero(18, "sven"));
            heroes.Add(new Hero(19, "tiny"));
            heroes.Add(new Hero(20, "vengeful_spirit"));
            heroes.Add(new Hero(21, "windranger"));
            heroes.Add(new Hero(22, "zeus"));
            heroes.Add(new Hero(23, "kunkka"));
            heroes.Add(new Hero(25, "lina"));
            heroes.Add(new Hero(26, "lion"));
            heroes.Add(new Hero(27, "shadow_shaman"));
            heroes.Add(new Hero(28, "slardar"));
            heroes.Add(new Hero(29, "tidehunter"));
            heroes.Add(new Hero(30, "witch_doctor"));
            heroes.Add(new Hero(31, "lich"));
            heroes.Add(new Hero(32, "riki"));
            heroes.Add(new Hero(33, "enigma"));
            heroes.Add(new Hero(34, "tinker"));
            heroes.Add(new Hero(35, "sniper"));
            heroes.Add(new Hero(36, "necrophos"));
            heroes.Add(new Hero(37, "warlock"));
            heroes.Add(new Hero(38, "beastmaster"));
            heroes.Add(new Hero(39, "queen_of_pain"));
            heroes.Add(new Hero(40, "venomancer"));
            heroes.Add(new Hero(41, "faceless_void"));
            heroes.Add(new Hero(42, "wraith_king"));
            heroes.Add(new Hero(43, "death_prophet"));
            heroes.Add(new Hero(44, "phantom_assassin"));
            heroes.Add(new Hero(45, "pugna"));
            heroes.Add(new Hero(46, "templar_assassin"));
            heroes.Add(new Hero(47, "viper"));
            heroes.Add(new Hero(48, "luna"));
            heroes.Add(new Hero(49, "dragon_knight"));
            heroes.Add(new Hero(50, "dazzle"));
            heroes.Add(new Hero(51, "clockwerk"));
            heroes.Add(new Hero(52, "leshrac"));
            heroes.Add(new Hero(53, "natures_prophet"));
            heroes.Add(new Hero(54, "lifestealer"));
            heroes.Add(new Hero(55, "dark_seer"));
            heroes.Add(new Hero(56, "clinkz"));
            heroes.Add(new Hero(57, "omniknight"));
            heroes.Add(new Hero(58, "enchantress"));
            heroes.Add(new Hero(59, "huskar"));
            heroes.Add(new Hero(60, "night_stalker"));
            heroes.Add(new Hero(61, "broodmother"));
            heroes.Add(new Hero(62, "bounty_hunter"));
            heroes.Add(new Hero(63, "weaver"));
            heroes.Add(new Hero(64, "jakiro"));
            heroes.Add(new Hero(65, "batrider"));
            heroes.Add(new Hero(66, "chen"));
            heroes.Add(new Hero(67, "spectre"));
            heroes.Add(new Hero(68, "ancient_apparition"));
            heroes.Add(new Hero(69, "doom"));
            heroes.Add(new Hero(70, "ursa"));
            heroes.Add(new Hero(71, "spirit_breaker"));
            heroes.Add(new Hero(72, "gyrocopter"));
            heroes.Add(new Hero(73, "alchemist"));
            heroes.Add(new Hero(74, "invoker"));
            heroes.Add(new Hero(75, "silencer"));
            heroes.Add(new Hero(76, "outworld_devourer"));
            heroes.Add(new Hero(77, "lycan"));
            heroes.Add(new Hero(78, "brewmaster"));
            heroes.Add(new Hero(79, "shadow_demon"));
            heroes.Add(new Hero(80, "lone_druid"));
            heroes.Add(new Hero(81, "chaos_knight"));
            heroes.Add(new Hero(82, "meepo"));
            heroes.Add(new Hero(83, "treant_protector"));
            heroes.Add(new Hero(84, "ogre_magi"));
            heroes.Add(new Hero(85, "undying"));
            heroes.Add(new Hero(86, "rubick"));
            heroes.Add(new Hero(87, "disruptor"));
            heroes.Add(new Hero(88, "nyx_assassin"));
            heroes.Add(new Hero(89, "naga_siren"));
            heroes.Add(new Hero(90, "keeper_of_the_light"));
            heroes.Add(new Hero(91, "io"));
            heroes.Add(new Hero(92, "visage"));
            heroes.Add(new Hero(93, "slark"));
            heroes.Add(new Hero(94, "medusa"));
            heroes.Add(new Hero(95, "troll_warlord"));
            heroes.Add(new Hero(96, "centaur_warrunner"));
            heroes.Add(new Hero(97, "magnus"));
            heroes.Add(new Hero(98, "timbersaw"));
            heroes.Add(new Hero(99, "bristleback"));
            heroes.Add(new Hero(100, "tusk"));
            heroes.Add(new Hero(101, "skywrath_mage"));
            heroes.Add(new Hero(102, "abaddon"));
            heroes.Add(new Hero(103, "elder_titan"));
            heroes.Add(new Hero(104, "legion_commander"));
            heroes.Add(new Hero(105, "techies"));
            heroes.Add(new Hero(106, "ember_spirit"));
            heroes.Add(new Hero(107, "earth_spirit"));
            heroes.Add(new Hero(108, "underlord"));
            heroes.Add(new Hero(109, "terrorblade"));
            heroes.Add(new Hero(110, "phoenix"));
            heroes.Add(new Hero(111, "oracle"));
            heroes.Add(new Hero(112, "winter_wyvern"));
            heroes.Add(new Hero(113, "arc_warden"));
            heroes.Add(new Hero(114, "monkey_king"));
            heroes.Add(new Hero(119, "dark_willow"));
            heroes.Add(new Hero(120, "pangolier"));
            heroes.Add(new Hero(121, "grimstroke"));
            heroes.Add(new Hero(129, "mars"));
        }
        
        private void RadiantPick(string heroName) {

            if (RadiantLabel1.Text.Equals("Radiant1"))
            {
                RadiantLabel1.Invoke(new Action(() => RadiantLabel1.Text = heroName));
            } else if (RadiantLabel2.Text.Equals("Radiant2"))
            {
                RadiantLabel2.Invoke(new Action(() => RadiantLabel2.Text = heroName));
            } else if (RadiantLabel3.Text.Equals("Radiant3"))
            {
                RadiantLabel3.Invoke(new Action(() => RadiantLabel3.Text = heroName));
            } else if (RadiantLabel4.Text.Equals("Radiant4"))
            {
                RadiantLabel4.Invoke(new Action(() => RadiantLabel4.Text = heroName));
            } else if (RadiantLabel5.Text.Equals("Radiant5"))
            {
                RadiantLabel5.Invoke(new Action(() => RadiantLabel5.Text = heroName));
            }
        }
        
        private void DirePick(string heroName) {
            if (DireLabel1.Text.Equals("Dire1"))
            {
                DireLabel1.Invoke(new Action(() => DireLabel1.Text = heroName));
            } else if (DireLabel2.Text.Equals("Dire2"))
            {
                DireLabel2.Invoke(new Action(() => DireLabel2.Text = heroName));
            } else if (DireLabel3.Text.Equals("Dire3"))
            {
                DireLabel3.Invoke(new Action(() => DireLabel3.Text = heroName));
            } else if (DireLabel4.Text.Equals("Dire4"))
            {
                DireLabel4.Invoke(new Action(() => DireLabel4.Text = heroName));
            } else if (DireLabel5.Text.Equals("Dire5"))
            {
                DireLabel5.Invoke(new Action(() => DireLabel5.Text = heroName));
            }
        }

        private void ResetLabels()
        {
            RadiantLabel1.Invoke(new Action(() => RadiantLabel1.Text = "Radiant1"));
            RadiantLabel2.Invoke(new Action(() => RadiantLabel2.Text = "Radiant2"));
            RadiantLabel3.Invoke(new Action(() => RadiantLabel3.Text = "Radiant3"));
            RadiantLabel4.Invoke(new Action(() => RadiantLabel4.Text = "Radiant4"));
            RadiantLabel5.Invoke(new Action(() => RadiantLabel5.Text = "Radiant5"));
            
//            RadiantLabel1.Invoke(new Action(() => RadiantLabel1.Visible = false));
//            RadiantLabel2.Invoke(new Action(() => RadiantLabel2.Visible = false));
//            RadiantLabel3.Invoke(new Action(() => RadiantLabel3.Visible = false));
//            RadiantLabel4.Invoke(new Action(() => RadiantLabel4.Visible = false));
//            RadiantLabel5.Invoke(new Action(() => RadiantLabel5.Visible = false));

            DireLabel1.Invoke(new Action(() => DireLabel1.Text = "Dire1"));
            DireLabel2.Invoke(new Action(() => DireLabel2.Text = "Dire2"));
            DireLabel3.Invoke(new Action(() => DireLabel3.Text = "Dire3"));
            DireLabel4.Invoke(new Action(() => DireLabel4.Text = "Dire4"));
            DireLabel5.Invoke(new Action(() => DireLabel5.Text = "Dire5"));
            
//            DireLabel1.Invoke(new Action(() => DireLabel1.Visible = false));
//            DireLabel2.Invoke(new Action(() => DireLabel2.Visible = false));
//            DireLabel3.Invoke(new Action(() => DireLabel3.Visible = false));
//            DireLabel4.Invoke(new Action(() => DireLabel4.Visible = false));
//            DireLabel5.Invoke(new Action(() => DireLabel5.Visible = false));
        }

    }
}
