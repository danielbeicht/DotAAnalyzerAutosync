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
        private static string accountID;
        private static DotaGSIHandler gsiHandler;
        
        private Bitmap[] lastScreenshotRadiant = new Bitmap[5];
        private Bitmap[] lastScreenshotDire = new Bitmap[5];

        private Rectangle[] radiantSection = new Rectangle[5];
        private Rectangle[] direSection = new Rectangle[5];

        private PictureBox[] radiantPictureBox = new PictureBox[5];
        private PictureBox[] direPictureBox = new PictureBox[5];

        private Label[] radiantLabel = new Label[5];
        private Label[] direLabel = new Label[5];

        private static DotaAnalyzerAutosyncForm instance = null;
        
        
        public DotaAnalyzerAutosyncForm()
        {
            InitializeComponent();

            InitializeProgramVariables();
            gsiHandler = DotaGSIHandler.Instance;

            // Initialize timer to call screenshot/check methods periodically every second
            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
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

        // Gets called when DotA 2 GSI detects pick phase
        public void StartTracking(string acID)
        {
            lblStatus.Invoke(new Action(() => lblStatus.Text = "Pick phase in progress. Tracking..."));
            trackingStopButton.Invoke(new Action(() => trackingStopButton.Enabled = true));
            ResetLabelsAndImages();

            accountID = acID;

            foreach (Hero hero in heroes)
            {
                hero.alreadyDetected = false;
            }

            string json = "{\"accountID\":\"" + accountID + "\"" + "}";
            PostRequest("newMatch", json);
            aTimer.Enabled = true;
        }

        // Gets called when DotA 2 GSI detects strategy phase
        public void StopTracking()
        {
            if (aTimer.Enabled)
            {
                aTimer.Enabled = false;
                Thread.Sleep(1500);
                OnTimedEvent(null, null);
                lblStatus.Invoke(new Action(() => lblStatus.Text = "Waiting for pick phase."));
                trackingStopButton.Invoke(new Action(() => trackingStopButton.Enabled = false));
            }
        }




        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            // Take screenshot of the upper screen
            Bitmap screenshotBitmap = Screenshot();

            // Get/Crop radiant images
            Bitmap[] radiantBitmap = new Bitmap[5];
            for (int i = 0; i < radiantBitmap.Length; i++)
            {
                radiantBitmap[i] = CropImage(screenshotBitmap, radiantSection[i]);
            }

            // Get/Crop dire images
            Bitmap[] direBitmap = new Bitmap[5];
            for (int i = 0; i < radiantBitmap.Length; i++)
            {
                direBitmap[i] = CropImage(screenshotBitmap, direSection[i]);
            }

            
            for (int i = 0; i < 5; i++)
            {
                // Check if current screenshot of radiant images differs from last screenshot and search for new heroes if a change was detected
                if (lastScreenshotRadiant[i] != null)
                {
                    if (ImageHasChanged(radiantBitmap[i], lastScreenshotRadiant[i]))
                    {
                        SearchImageForHero(radiantBitmap[i], true, i);
                    }
                }
                else
                {
                    SearchImageForHero(radiantBitmap[i], true, i);
                }
                lastScreenshotRadiant[i] = radiantBitmap[i].Clone(new Rectangle(0, 0, radiantBitmap[i].Width, radiantBitmap[i].Height), radiantBitmap[i].PixelFormat);

                // Check if current screenshot of dire images differs from last screenshot and search for new heroes if a change was detected
                if (lastScreenshotDire[i] != null)
                {
                    if (ImageHasChanged(direBitmap[i], lastScreenshotDire[i]))
                    {
                        SearchImageForHero(direBitmap[i], false, i);
                    }
                }
                else
                {
                    SearchImageForHero(direBitmap[i], false, i);
                }
                lastScreenshotDire[i] = direBitmap[i].Clone(new Rectangle(0, 0, direBitmap[i].Width, direBitmap[i].Height), direBitmap[i].PixelFormat);
            }

            // Display images in windows form
            for (int i = 0; i < 5; i++)
            {
                radiantPictureBox[i].Invoke(new Action(() => radiantPictureBox[i].Image = radiantBitmap[i].Clone(new Rectangle(0, 0, radiantBitmap[i].Width, radiantBitmap[i].Height), radiantBitmap[i].PixelFormat)));
                radiantPictureBox[i].Invoke(new Action(() => radiantPictureBox[i].SizeMode = PictureBoxSizeMode.AutoSize));
                direPictureBox[i].Invoke(new Action(() => direPictureBox[i].Image = direBitmap[i].Clone(new Rectangle(0, 0, direBitmap[i].Width, direBitmap[i].Height), direBitmap[i].PixelFormat)));
                direPictureBox[i].Invoke(new Action(() => direPictureBox[i].SizeMode = PictureBoxSizeMode.AutoSize));
            }
        }

        // Creates new Thread for every hero search
        private void SearchImageForHero(Bitmap b, bool isRadiant, int playerNumber)
        {
            Image<Bgr, Byte> screenshot = new Image<Bgr, Byte>(b);

            List<Task> tasks = new List<Task>();

            foreach (Hero hero in heroes)
            {
                if (hero.image != null && !hero.alreadyDetected)
                {
                    Task task = Task.Factory.StartNew(() => SearchHero(hero, screenshot, isRadiant, playerNumber));
                    tasks.Add(task);
                }
            }
            Task.WaitAll(tasks.ToArray());

            screenshot.Dispose();
        }

        // Looks for hero on cropped image
        private void SearchHero(Hero hero, Image<Bgr, Byte> screenshot, bool isRadiant, int playerNumber)
        {
            Image<Gray, float> result = screenshot.MatchTemplate(hero.image, TemplateMatchingType.SqdiffNormed);

            double min = 0, max = 0;
            Point maxp = new Point(0, 0);
            Point minp = new Point(0, 0);
            CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

            if (min < 0.01)
            {
                hero.alreadyDetected = true;

                string isRadiantString = "false";

                if (isRadiant)
                {
                    isRadiantString = "true";
                }

                string json = "{\"accountID\":\"" + accountID + "\"," +
                                  "\"heroID\":" + hero.id +
                                  ",\"isRadiant\":" + isRadiantString + "}";

                PostRequest("addHeroToMatch", json);
                if (isRadiantString.Equals("true"))
                {
                    RadiantPick(hero.name, playerNumber);
                }
                else
                {
                    DirePick(hero.name, playerNumber);
                }
            }

            result.Dispose();
        }


        // Take screenshot and crop it to pick area
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

            return screen;
        }

        // Checks if two bitmaps have a major difference
        private bool ImageHasChanged(Bitmap newImage, Bitmap oldImage)
        {
            Image<Bgr, Byte> lScreen = new Image<Bgr, Byte>(oldImage);
            Image<Bgr, Byte> cScreen = new Image<Bgr, Byte>(newImage);
            Image<Gray, float> result = cScreen.MatchTemplate(lScreen, TemplateMatchingType.SqdiffNormed);

            double min = 0, max = 0;
            Point maxp = new Point(0, 0);
            Point minp = new Point(0, 0);
            CvInvoke.MinMaxLoc(result, ref min, ref max, ref minp, ref maxp);

            if (min < 0.01)
            {
                return false;
            }
            return true;
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

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }
        }


        private void InitializeProgramVariables()
        {
            radiantSection[0] = new Rectangle(new Point(292, 0), new Size(130, 94));
            radiantSection[1] = new Rectangle(new Point(457, 0), new Size(130, 94));
            radiantSection[2] = new Rectangle(new Point(622, 0), new Size(130, 94));
            radiantSection[3] = new Rectangle(new Point(787, 0), new Size(130, 94));
            radiantSection[4] = new Rectangle(new Point(952, 0), new Size(130, 94));
            
            direSection[0] = new Rectangle(new Point(1476, 0), new Size(130, 94));
            direSection[1] = new Rectangle(new Point(1641, 0), new Size(130, 94));
            direSection[2] = new Rectangle(new Point(1806, 0), new Size(130, 94));
            direSection[3] = new Rectangle(new Point(1971, 0), new Size(130, 94));
            direSection[4] = new Rectangle(new Point(2136, 0), new Size(130, 94));

            radiantPictureBox[0] = radiant1PictureBox;
            radiantPictureBox[1] = radiant2PictureBox;
            radiantPictureBox[2] = radiant3PictureBox;
            radiantPictureBox[3] = radiant4PictureBox;
            radiantPictureBox[4] = radiant5PictureBox;

            direPictureBox[0] = dire1PictureBox;
            direPictureBox[1] = dire2PictureBox;
            direPictureBox[2] = dire3PictureBox;
            direPictureBox[3] = dire4PictureBox;
            direPictureBox[4] = dire5PictureBox;

            radiantLabel[0] = RadiantLabel1;
            radiantLabel[1] = RadiantLabel2;
            radiantLabel[2] = RadiantLabel3;
            radiantLabel[3] = RadiantLabel4;
            radiantLabel[4] = RadiantLabel5;

            direLabel[0] = DireLabel1;
            direLabel[1] = DireLabel2;
            direLabel[2] = DireLabel3;
            direLabel[3] = DireLabel4;
            direLabel[4] = DireLabel5;

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
        
        private void RadiantPick(string heroName, int playerNumber)
        {

            if (radiantLabel[playerNumber].Text.Equals("Radiant" + (playerNumber + 1)))
            {
                radiantLabel[playerNumber].Invoke(new Action(() => radiantLabel[playerNumber].Text = heroName));
            }

        }
        
        private void DirePick(string heroName, int playerName) {
            if (direLabel[playerName].Text.Equals("Dire" + (playerName + 1)))
            {
                direLabel[playerName].Invoke(new Action(() => direLabel[playerName].Text = heroName));
            }
        }

        private void ResetLabelsAndImages()
        {
            for (int i=0; i<5; i++)
            {
                radiantLabel[i].Invoke(new Action(() => radiantLabel[i].Text = "Radiant" + (i+1)));
                direLabel[i].Invoke(new Action(() => direLabel[i].Text = "Dire" + (i+1)));
                radiantPictureBox[i].Invoke(new Action(() => radiantPictureBox[i].Image = null));
                direPictureBox[i].Invoke(new Action(() => direPictureBox[i].Image = null));
            }
        }

        private void trackingStopButton_Click(object sender, EventArgs e)
        {
            StopTracking();
        }
    }
}
