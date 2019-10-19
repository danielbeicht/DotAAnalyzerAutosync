using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
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
        
        private readonly System.Timers.Timer _aTimer;
        private List<Hero> _heroes;
        private string _accountId;
        private DotaGSIHandler gsiHandler;
        
        private Bitmap[] _lastScreenshotRadiant = new Bitmap[5];
        private Bitmap[] _lastScreenshotDire = new Bitmap[5];

        private Rectangle[] _radiantSection = new Rectangle[5];
        private Rectangle[] _direSection = new Rectangle[5];

        private PictureBox[] _radiantPictureBox = new PictureBox[5];
        private PictureBox[] _direPictureBox = new PictureBox[5];

        private Label[] _radiantLabel = new Label[5];
        private Label[] _direLabel = new Label[5];

        private static DotaAnalyzerAutosyncForm _instance = null;


        private DotaAnalyzerAutosyncForm()
        {
            InitializeComponent();

            InitializeProgramVariables();
            gsiHandler = DotaGSIHandler.Instance;

            // Initialize timer to call screenshot/check methods periodically every second
            _aTimer = new System.Timers.Timer(1000);
            _aTimer.Elapsed += OnTimedEvent;
            _aTimer.AutoReset = true;
        }
        
        
        public static DotaAnalyzerAutosyncForm Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DotaAnalyzerAutosyncForm();
                }
                return _instance;
            }
        }

        // Gets called when DotA 2 GSI detects pick phase
        public void StartTracking(string acID)
        {
            lblStatus.Invoke(new Action(() => lblStatus.Text = "Pick phase in progress. Tracking..."));
            trackingStopButton.Invoke(new Action(() => trackingStopButton.Enabled = true));
            ResetLabelsAndImages();

            _accountId = acID;

            foreach (Hero hero in _heroes)
            {
                hero.alreadyDetected = false;
            }

            string json = "{\"accountID\":\"" + _accountId + "\"" + "}";
            PostRequest("newMatch", json);
            _aTimer.Enabled = true;
        }

        // Gets called when DotA 2 GSI detects strategy phase
        public void StopTracking()
        {
            if (_aTimer.Enabled)
            {
                _aTimer.Enabled = false;
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
                radiantBitmap[i] = CropImage(screenshotBitmap, _radiantSection[i]);
            }

            // Get/Crop dire images
            Bitmap[] direBitmap = new Bitmap[5];
            for (int i = 0; i < radiantBitmap.Length; i++)
            {
                direBitmap[i] = CropImage(screenshotBitmap, _direSection[i]);
            }

            
            for (int i = 0; i < 5; i++)
            {
                // Check if current screenshot of radiant images differs from last screenshot and search for new heroes if a change was detected
                if (_lastScreenshotRadiant[i] != null)
                {
                    if (ImageHasChanged(radiantBitmap[i], _lastScreenshotRadiant[i]))
                    {
                        SearchImageForHero(radiantBitmap[i], true, i);
                    }
                }
                else
                {
                    SearchImageForHero(radiantBitmap[i], true, i);
                }
                _lastScreenshotRadiant[i] = radiantBitmap[i].Clone(new Rectangle(0, 0, radiantBitmap[i].Width, radiantBitmap[i].Height), radiantBitmap[i].PixelFormat);

                // Check if current screenshot of dire images differs from last screenshot and search for new heroes if a change was detected
                if (_lastScreenshotDire[i] != null)
                {
                    if (ImageHasChanged(direBitmap[i], _lastScreenshotDire[i]))
                    {
                        SearchImageForHero(direBitmap[i], false, i);
                    }
                }
                else
                {
                    SearchImageForHero(direBitmap[i], false, i);
                }
                _lastScreenshotDire[i] = direBitmap[i].Clone(new Rectangle(0, 0, direBitmap[i].Width, direBitmap[i].Height), direBitmap[i].PixelFormat);
            }

            // Display images in windows form
            for (int i = 0; i < 5; i++)
            {
                _radiantPictureBox[i].Invoke(new Action(() => _radiantPictureBox[i].Image = radiantBitmap[i].Clone(new Rectangle(0, 0, radiantBitmap[i].Width, radiantBitmap[i].Height), radiantBitmap[i].PixelFormat)));
                _radiantPictureBox[i].Invoke(new Action(() => _radiantPictureBox[i].SizeMode = PictureBoxSizeMode.AutoSize));
                _direPictureBox[i].Invoke(new Action(() => _direPictureBox[i].Image = direBitmap[i].Clone(new Rectangle(0, 0, direBitmap[i].Width, direBitmap[i].Height), direBitmap[i].PixelFormat)));
                _direPictureBox[i].Invoke(new Action(() => _direPictureBox[i].SizeMode = PictureBoxSizeMode.AutoSize));
            }
        }

        // Creates new Thread for every hero search
        private void SearchImageForHero(Bitmap b, bool isRadiant, int playerNumber)
        {
            Image<Bgr, Byte> screenshot = new Image<Bgr, Byte>(b);

            List<Task> tasks = new List<Task>();

            foreach (Hero hero in _heroes)
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

                string json = "{\"accountID\":\"" + _accountId + "\"," +
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
            _radiantSection[0] = new Rectangle(new Point(292, 0), new Size(130, 94));
            _radiantSection[1] = new Rectangle(new Point(457, 0), new Size(130, 94));
            _radiantSection[2] = new Rectangle(new Point(622, 0), new Size(130, 94));
            _radiantSection[3] = new Rectangle(new Point(787, 0), new Size(130, 94));
            _radiantSection[4] = new Rectangle(new Point(952, 0), new Size(130, 94));
            
            _direSection[0] = new Rectangle(new Point(1476, 0), new Size(130, 94));
            _direSection[1] = new Rectangle(new Point(1641, 0), new Size(130, 94));
            _direSection[2] = new Rectangle(new Point(1806, 0), new Size(130, 94));
            _direSection[3] = new Rectangle(new Point(1971, 0), new Size(130, 94));
            _direSection[4] = new Rectangle(new Point(2136, 0), new Size(130, 94));

            _radiantPictureBox[0] = radiant1PictureBox;
            _radiantPictureBox[1] = radiant2PictureBox;
            _radiantPictureBox[2] = radiant3PictureBox;
            _radiantPictureBox[3] = radiant4PictureBox;
            _radiantPictureBox[4] = radiant5PictureBox;

            _direPictureBox[0] = dire1PictureBox;
            _direPictureBox[1] = dire2PictureBox;
            _direPictureBox[2] = dire3PictureBox;
            _direPictureBox[3] = dire4PictureBox;
            _direPictureBox[4] = dire5PictureBox;

            _radiantLabel[0] = RadiantLabel1;
            _radiantLabel[1] = RadiantLabel2;
            _radiantLabel[2] = RadiantLabel3;
            _radiantLabel[3] = RadiantLabel4;
            _radiantLabel[4] = RadiantLabel5;

            _direLabel[0] = DireLabel1;
            _direLabel[1] = DireLabel2;
            _direLabel[2] = DireLabel3;
            _direLabel[3] = DireLabel4;
            _direLabel[4] = DireLabel5;

            _heroes = new List<Hero>();
            _heroes.Add(new Hero(1, "anti_mage"));
            _heroes.Add(new Hero(2, "axe"));
            _heroes.Add(new Hero(3, "bane"));
            _heroes.Add(new Hero(4, "bloodseeker"));
            _heroes.Add(new Hero(5, "crystal_maiden"));
            _heroes.Add(new Hero(6, "drow_ranger"));
            _heroes.Add(new Hero(7, "earthshaker"));
            _heroes.Add(new Hero(8, "juggernaut"));
            _heroes.Add(new Hero(9, "mirana"));
            _heroes.Add(new Hero(10, "morphling"));
            _heroes.Add(new Hero(11, "shadow_fiend"));
            _heroes.Add(new Hero(12, "phantom_lancer"));
            _heroes.Add(new Hero(13, "puck"));
            _heroes.Add(new Hero(14, "pudge"));
            _heroes.Add(new Hero(15, "razor"));
            _heroes.Add(new Hero(16, "sand_king"));
            _heroes.Add(new Hero(17, "storm_spirit"));
            _heroes.Add(new Hero(18, "sven"));
            _heroes.Add(new Hero(19, "tiny"));
            _heroes.Add(new Hero(20, "vengeful_spirit"));
            _heroes.Add(new Hero(21, "windranger"));
            _heroes.Add(new Hero(22, "zeus"));
            _heroes.Add(new Hero(23, "kunkka"));
            _heroes.Add(new Hero(25, "lina"));
            _heroes.Add(new Hero(26, "lion"));
            _heroes.Add(new Hero(27, "shadow_shaman"));
            _heroes.Add(new Hero(28, "slardar"));
            _heroes.Add(new Hero(29, "tidehunter"));
            _heroes.Add(new Hero(30, "witch_doctor"));
            _heroes.Add(new Hero(31, "lich"));
            _heroes.Add(new Hero(32, "riki"));
            _heroes.Add(new Hero(33, "enigma"));
            _heroes.Add(new Hero(34, "tinker"));
            _heroes.Add(new Hero(35, "sniper"));
            _heroes.Add(new Hero(36, "necrophos"));
            _heroes.Add(new Hero(37, "warlock"));
            _heroes.Add(new Hero(38, "beastmaster"));
            _heroes.Add(new Hero(39, "queen_of_pain"));
            _heroes.Add(new Hero(40, "venomancer"));
            _heroes.Add(new Hero(41, "faceless_void"));
            _heroes.Add(new Hero(42, "wraith_king"));
            _heroes.Add(new Hero(43, "death_prophet"));
            _heroes.Add(new Hero(44, "phantom_assassin"));
            _heroes.Add(new Hero(45, "pugna"));
            _heroes.Add(new Hero(46, "templar_assassin"));
            _heroes.Add(new Hero(47, "viper"));
            _heroes.Add(new Hero(48, "luna"));
            _heroes.Add(new Hero(49, "dragon_knight"));
            _heroes.Add(new Hero(50, "dazzle"));
            _heroes.Add(new Hero(51, "clockwerk"));
            _heroes.Add(new Hero(52, "leshrac"));
            _heroes.Add(new Hero(53, "natures_prophet"));
            _heroes.Add(new Hero(54, "lifestealer"));
            _heroes.Add(new Hero(55, "dark_seer"));
            _heroes.Add(new Hero(56, "clinkz"));
            _heroes.Add(new Hero(57, "omniknight"));
            _heroes.Add(new Hero(58, "enchantress"));
            _heroes.Add(new Hero(59, "huskar"));
            _heroes.Add(new Hero(60, "night_stalker"));
            _heroes.Add(new Hero(61, "broodmother"));
            _heroes.Add(new Hero(62, "bounty_hunter"));
            _heroes.Add(new Hero(63, "weaver"));
            _heroes.Add(new Hero(64, "jakiro"));
            _heroes.Add(new Hero(65, "batrider"));
            _heroes.Add(new Hero(66, "chen"));
            _heroes.Add(new Hero(67, "spectre"));
            _heroes.Add(new Hero(68, "ancient_apparition"));
            _heroes.Add(new Hero(69, "doom"));
            _heroes.Add(new Hero(70, "ursa"));
            _heroes.Add(new Hero(71, "spirit_breaker"));
            _heroes.Add(new Hero(72, "gyrocopter"));
            _heroes.Add(new Hero(73, "alchemist"));
            _heroes.Add(new Hero(74, "invoker"));
            _heroes.Add(new Hero(75, "silencer"));
            _heroes.Add(new Hero(76, "outworld_devourer"));
            _heroes.Add(new Hero(77, "lycan"));
            _heroes.Add(new Hero(78, "brewmaster"));
            _heroes.Add(new Hero(79, "shadow_demon"));
            _heroes.Add(new Hero(80, "lone_druid"));
            _heroes.Add(new Hero(81, "chaos_knight"));
            _heroes.Add(new Hero(82, "meepo"));
            _heroes.Add(new Hero(83, "treant_protector"));
            _heroes.Add(new Hero(84, "ogre_magi"));
            _heroes.Add(new Hero(85, "undying"));
            _heroes.Add(new Hero(86, "rubick"));
            _heroes.Add(new Hero(87, "disruptor"));
            _heroes.Add(new Hero(88, "nyx_assassin"));
            _heroes.Add(new Hero(89, "naga_siren"));
            _heroes.Add(new Hero(90, "keeper_of_the_light"));
            _heroes.Add(new Hero(91, "io"));
            _heroes.Add(new Hero(92, "visage"));
            _heroes.Add(new Hero(93, "slark"));
            _heroes.Add(new Hero(94, "medusa"));
            _heroes.Add(new Hero(95, "troll_warlord"));
            _heroes.Add(new Hero(96, "centaur_warrunner"));
            _heroes.Add(new Hero(97, "magnus"));
            _heroes.Add(new Hero(98, "timbersaw"));
            _heroes.Add(new Hero(99, "bristleback"));
            _heroes.Add(new Hero(100, "tusk"));
            _heroes.Add(new Hero(101, "skywrath_mage"));
            _heroes.Add(new Hero(102, "abaddon"));
            _heroes.Add(new Hero(103, "elder_titan"));
            _heroes.Add(new Hero(104, "legion_commander"));
            _heroes.Add(new Hero(105, "techies"));
            _heroes.Add(new Hero(106, "ember_spirit"));
            _heroes.Add(new Hero(107, "earth_spirit"));
            _heroes.Add(new Hero(108, "underlord"));
            _heroes.Add(new Hero(109, "terrorblade"));
            _heroes.Add(new Hero(110, "phoenix"));
            _heroes.Add(new Hero(111, "oracle"));
            _heroes.Add(new Hero(112, "winter_wyvern"));
            _heroes.Add(new Hero(113, "arc_warden"));
            _heroes.Add(new Hero(114, "monkey_king"));
            _heroes.Add(new Hero(119, "dark_willow"));
            _heroes.Add(new Hero(120, "pangolier"));
            _heroes.Add(new Hero(121, "grimstroke"));
            _heroes.Add(new Hero(129, "mars"));
        }
        
        private void RadiantPick(string heroName, int playerNumber)
        {

            if (_radiantLabel[playerNumber].Text.Equals("Radiant" + (playerNumber + 1)))
            {
                _radiantLabel[playerNumber].Invoke(new Action(() => _radiantLabel[playerNumber].Text = heroName));
            }

        }
        
        private void DirePick(string heroName, int playerName) {
            if (_direLabel[playerName].Text.Equals("Dire" + (playerName + 1)))
            {
                _direLabel[playerName].Invoke(new Action(() => _direLabel[playerName].Text = heroName));
            }
        }

        private void ResetLabelsAndImages()
        {
            for (int i=0; i<5; i++)
            {
                _radiantLabel[i].Invoke(new Action(() => _radiantLabel[i].Text = "Radiant" + (i+1)));
                _direLabel[i].Invoke(new Action(() => _direLabel[i].Text = "Dire" + (i+1)));
                _radiantPictureBox[i].Invoke(new Action(() => _radiantPictureBox[i].Image = null));
                _direPictureBox[i].Invoke(new Action(() => _direPictureBox[i].Image = null));
            }
        }

        private void trackingStopButton_Click(object sender, EventArgs e)
        {
            StopTracking();
        }
    }
}
