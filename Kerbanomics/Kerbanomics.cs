using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using KSP;

namespace Kerbanomics
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class Kerbanomics : MonoBehaviour
    {
        private String save_folder;
        private bool billing_enabled = true;
        private bool autopayEnabled = false;
        private int threshold = 50;
        private double autopayAmount = 0;
        private int level0 = 10;
        private int level1 = 20;
        private int level2 = 40;
        private int level3 = 80;
        private int level4 = 140;
        private int level5 = 200;
        private int standbyPct = 50;
        private bool yearly = false;

        private double bills = 0;
        private float loanAmount = 0;
        private float loanPayment = 0;
        float addPay = 0;
        int reqAmount = 0;
        float amountFinanced = 0;
        float estPayment = 0;
        int payments = 0;

        private ConfigNode settings;
        private ConfigNode values;

        double _interval = 2300400;
        public static int _lastUpdate = 0;
        Game game;
        private Rect settingsWindow = new Rect(Screen.height / 8 + 125, Screen.width / 4 , 300, 400);
        private Rect mainWindow = new Rect(Screen.width / 8 + 125, Screen.height / 4, 400, 125); 
        private Rect loanWindow = new Rect(Screen.width / 8 +125, Screen.height / 4 , 400, 125);
        public ApplicationLauncherButton button;
        public static Kerbanomics Instance;

        public void Awake()
        {
            game = HighLogic.CurrentGame;
            LoadSettings();
            if (GameSettings.KERBIN_TIME && yearly == false)
                _interval = 2300400;
            else if (!GameSettings.KERBIN_TIME && yearly == false)
                _interval = 7884000;
            else if (GameSettings.KERBIN_TIME && yearly == true)
                _interval = 9201600;
            else if (!GameSettings.KERBIN_TIME && yearly == true)
                _interval = 31536000;
            if (HighLogic.LoadedSceneHasPlanetarium)
                _lastUpdate = (int)Math.Floor(Planetarium.GetUniversalTime() / _interval);
        }

        public void Start()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            save_folder = GetRootPath() + "/saves/" + HighLogic.SaveFolder + "/";
            LoadSettings();
        }

        public void DestroyButtons()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            ApplicationLauncher.Instance.RemoveModApplication(button);
        }

        void Update()
        {
            
            if (HighLogic.LoadedSceneHasPlanetarium)
            {
                int currentPeriod = (int)Math.Floor(Planetarium.GetUniversalTime() / _interval);
                if (currentPeriod > _lastUpdate && billing_enabled == true)
                {
                    double multiplier = 106.5;
                    if (yearly == true)
                        multiplier = 426.08;
                    Debug.Log("_lastUpdate=" + _lastUpdate + ", currentDay=" + currentPeriod);
                    _lastUpdate = (currentPeriod);
                    StringBuilder message = new StringBuilder();
                    message.AppendLine("Payroll is processed.");
                    message.AppendLine("Current staff:");
                    foreach (ProtoCrewMember crewMember in game.CrewRoster.Crew)
                    {
                        double wage = 10;
                        double standbyWage = 5;
                        switch (crewMember.experienceLevel)
                        {
                            case 0:
                                wage = level0;
                                break;
                            case 1:
                                wage = level1;
                                break;
                            case 2:
                                wage = level2;
                                break;
                            case 3:
                                wage = level3;
                                break;
                            case 4:
                                wage = level4;
                                break;
                            case 5:
                                wage = level5;
                                break;
                            default:
                                wage = 10;
                                break;
                        }
                        standbyWage = standbyPct / 100 * wage;
                        message.Append(crewMember.name);
                        if (crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Assigned))
                        {
                            double paycheck = wage * multiplier;
                            message.AppendLine(", level" + crewMember.experienceLevel + ", is on mission. Wages paid = " + paycheck);
                            Funding.Instance.AddFunds(-paycheck, 0);
                        }
                        else if (crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Available))
                        {
                            double paycheck = standbyWage * multiplier;
                            message.AppendLine(", level" + crewMember.experienceLevel + ", is available. Wages paid = " + paycheck);
                            Funding.Instance.AddFunds(-paycheck, 0);
                        }
                    }
                    Funding.Instance.AddFunds(-PayLoan(loanPayment), 0);
                    message.AppendLine("Thank you for your loan payment in the amount of " + loanPayment + "! Have a pleasant day!");
                    double externalFunding = 2500 * Reputation.CurrentRep;
                    if (yearly == true)
                        externalFunding = 10000 * Reputation.CurrentRep;
                    Funding.Instance.AddFunds(+externalFunding, 0);
                    message.AppendLine("Received Funding - " + externalFunding);
                    SaveData();
                    MessageSystem.Message m = new MessageSystem.Message(
                        "Processing Finances",
                        message.ToString(),
                        MessageSystemButton.MessageButtonColor.RED,
                        MessageSystemButton.ButtonIcons.ALERT);
                    MessageSystem.Instance.AddMessage(m);
                }
            }
        }

        public void OnDestroy()
        {
            DestroyButtons();
        }

        void OnGUIAppLauncherReady()
        {
            this.button = ApplicationLauncher.Instance.AddModApplication(
                onAppLauncherToggleOn,
                onAppLauncherToggleOff,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.SPACECENTER,
                (Texture)GameDatabase.Instance.GetTexture("EvilCorp/Textures/icon_button_stock", false));
        }
               
        void onAppLauncherToggleOn()
        {
            Debug.Log("Toggled on");
            RenderingManager.AddToPostDrawQueue(0, OnDraw);
            Debug.Log("Saving config to" + save_folder);
            Debug.Log("billing_enabled = " + billing_enabled);
            Debug.Log("autopayEnabled = " + autopayEnabled);
            Debug.Log("threshold = " + threshold);
            Debug.Log("autopayAmount = " + autopayAmount);
            Debug.Log("level0 = " + level0);
            Debug.Log("level1 = " + level1);
            Debug.Log("level2 = " + level2);
            Debug.Log("level3 = " + level3);
            Debug.Log("level4 = " + level4);
            Debug.Log("level5 = " + level5);
            Debug.Log("standbyPct = " + standbyPct);
            Debug.Log("yearly = " + yearly);
            //ResetToDefault();
        }

        private void OnDraw()
        {
            mainWindow = GUILayout.Window(854123, mainWindow, OnWindow, "Kerbanomics"); 
        }

        private void DrawSettings()
        {
            settingsWindow = GUILayout.Window(912361, settingsWindow, SettingsWind, "Kerbanomics Settings");
        }

        private void DrawLoanWindow()
        {
            if (loanAmount > 0)
            {
                loanWindow = GUILayout.Window(87441, loanWindow, LoanWindLayoutDeny, "Loans");
            }
            else
            {
                loanWindow = GUILayout.Window(56489, loanWindow, LoanWindLayoutApprove, "Loans");
            }
        }

        private void OnWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Outstanding Bills: " + bills.ToString());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Loan Balance: " + loanAmount.ToString());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scheduled Loan Payment: " + loanPayment.ToString());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Loans", GUILayout.ExpandWidth(true)))
            {
                RenderingManager.AddToPostDrawQueue(0, DrawLoanWindow);
            }
            if(GUILayout.Button("Settings", GUILayout.ExpandWidth(true)))
            {
                RenderingManager.AddToPostDrawQueue(0, DrawSettings);
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void SettingsWind(int windowId)
        {
            GUILayout.BeginHorizontal();
            billing_enabled = GUILayout.Toggle(billing_enabled, "Enabled");
            GUILayout.FlexibleSpace();
            yearly = GUILayout.Toggle(yearly, "Yearly Billing");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Maximum % of funds paid per period: ");
            GUILayout.FlexibleSpace();
            threshold = Convert.ToInt32(GUILayout.TextField(threshold.ToString(), 3, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 0 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level0 = Convert.ToInt32(GUILayout.TextField(level0.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 1 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level1 = Convert.ToInt32(GUILayout.TextField(level1.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 2 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level2 = Convert.ToInt32(GUILayout.TextField(level2.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 3 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level3 = Convert.ToInt32(GUILayout.TextField(level3.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 4 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level4 = Convert.ToInt32(GUILayout.TextField(level4.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level 5 Daily Wages: ");
            GUILayout.FlexibleSpace();
            level5 = Convert.ToInt32(GUILayout.TextField(level5.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Percentage of Daily Wage for Standby: ");
            GUILayout.FlexibleSpace();
            standbyPct = Convert.ToInt32(GUILayout.TextField(standbyPct.ToString(), 4, GUILayout.Width(50)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.ExpandWidth(true)))
            {
                SaveSettings();
            }
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(true)))
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawSettings);
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void LoanWindLayoutApprove(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Requested Amount: ");
            GUILayout.FlexibleSpace();
            reqAmount = Convert.ToInt32(GUILayout.TextField(reqAmount.ToString(), 7, GUILayout.Width(75)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Interest Rate: " + CalcInterest() + "%");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("How many payments (Max 99)? ");
            GUILayout.FlexibleSpace();
            payments = Convert.ToInt32(GUILayout.TextField(payments.ToString(), 2, GUILayout.Width(75)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Estimated Payment Amount: " + estPayment);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total amount financed: " + amountFinanced);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Calculate"))
            {
                float intMult = CalcInterest() / 100;
                amountFinanced = reqAmount * (intMult + 1);
                estPayment = amountFinanced / payments;
                Debug.Log("Financed: " + amountFinanced);
                Debug.Log("Estimated Payment: " + estPayment);
                Debug.Log("Interest: " + intMult);
                
                //Here is a set of calculations using compound interest instead of the above calcs
                float APR = CalcInterest();
                //50% annual rates are HUGE.  A more sensible range of rates might be 3-20%, for now let's use the existing code
                //there are 4 periods per year, so the period percentage is:
                float periodRate = 100*(Math.Pow((1+APR/100),(1.0/4))-1); //gives the rate in %
                //the next calculation gives the payment value including the interest accrued over the life of the loan
                float paymentValue = (1.0/(periodRate/100))*(1 - 1.0/(Math.Pow((1+periodRate/100),payments)));
                
                //the estimated payment is the loan principle (required amount) divided by the payment value
                estPayment = reqAmount / paymentValue;
                
                //the total loan to be repayed is given by the estimated payment times the number of periods for the life of the loan
                amountFinanced = estPayment*payments; 
                Debug.Log("Compound Financed: " + amountFinanced);
                Debug.Log("Estimated Payment: " + estPayment);
                Debug.Log("Annual Interest Rate (APR): " + APR);
                Debug.Log("paymentValue: " + paymentValue);
                /*what should really be done is to keep track of the per period interest rate and the following steps
                would look like a real loan:
                At the loan disbursement, apply the value of the loan, only, as the financed amount.
                Each update period the following would be calculated: interest amount based on the periodRate calculated above.
                The interest rate is added to the financed amount for this period.
                Then, the player would be asked if they authorize the standard payment for the period (i.e. the estPayment).
                IF the player only payed the estPayment each period, the loan would eventually be payed back
                (including the compound interest).  An option should probably be included for the player to provide a larger payment 
                than the estPayment to pay down more of the loan principle.
                A simple check making the max payment no larger than the remaining principle*(1+periodRate) would eliminate overpaying
                the loan.
                */
            }
            if (amountFinanced != 0)
            {
                if (GUILayout.Button("Accept"))
                {
                    Funding.Instance.AddFunds(reqAmount, 0);
                    loanAmount = amountFinanced;
                    loanPayment = amountFinanced / payments;
                    SaveData();
                    RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
                }
            }
            if (GUILayout.Button("Close"))
                RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void LoanWindLayoutDeny(int windowId)
        {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("You've already borrowed money, you have to pay off your original loan before borrowing more!");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Additional Loan Payment: ");
                addPay = Convert.ToInt32(GUILayout.TextField(loanPayment.ToString(), 7));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Pay", GUILayout.ExpandWidth(true)))
                {
                    Funding.Instance.AddFunds(-addPay, 0);
                    PayLoan(addPay);
                    RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
                }
                if(GUILayout.Button("Close", GUILayout.ExpandWidth(true)))
                {
                    RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
                }
                GUILayout.EndHorizontal();
                GUI.DragWindow();
        }

        void onAppLauncherToggleOff()
        {
            Debug.Log("Toggled off");
            RenderingManager.RemoveFromPostDrawQueue(0, OnDraw);
        }

        public static String GetRootPath()
        {
            String path = KSPUtil.ApplicationRootPath;
            path = path.Replace("\\", "/");
            if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
            return path;
        }

        private void SaveSettings()
        {
            settings = new ConfigNode();
            settings.name = "SETTINGS";
            settings.AddValue("Enabled", billing_enabled);
            settings.AddValue("Yearly", yearly);
            settings.AddValue("Threshold", threshold);
            settings.AddValue("Autopay", autopayEnabled);
            settings.AddValue("AutopayAmount", autopayAmount);
            settings.AddValue("WagesLevel0", level0);
            settings.AddValue("WagesLevel1", level1);
            settings.AddValue("WagesLevel2", level2);
            settings.AddValue("WagesLevel3", level3);
            settings.AddValue("WagesLevel4", level4);
            settings.AddValue("WagesLevel5", level5);
            settings.AddValue("StandbyPercentage", standbyPct);

            settings.Save(save_folder + "Settings.cfg");
        }

        private void LoadSettings()
        {
            settings = new ConfigNode();
            settings = ConfigNode.Load(save_folder + "Settings.cfg");
            if (settings != null)
            {
                if (settings.HasValue("Enabled")) billing_enabled = Boolean.Parse(settings.GetValue("Enabled"));
                if (settings.HasValue("Yearly")) yearly = Boolean.Parse(settings.GetValue("Yearly"));
                if (settings.HasValue("Threshold")) threshold = (Int32)Int32.Parse(settings.GetValue("Threshold"));
                if (settings.HasValue("Autopay")) autopayEnabled = Boolean.Parse(settings.GetValue("Autopay"));
                if (settings.HasValue("AutopayAmount")) autopayAmount = (Double)Double.Parse(settings.GetValue("AutopayAmount"));
                if (settings.HasValue("WagesLevel0")) level0 = (Int32)Int32.Parse(settings.GetValue("WagesLevel0"));
                if (settings.HasValue("WagesLevel1")) level1 = (Int32)Int32.Parse(settings.GetValue("WagesLevel1"));
                if (settings.HasValue("WagesLevel2")) level2 = (Int32)Int32.Parse(settings.GetValue("WagesLevel2"));
                if (settings.HasValue("WagesLevel3")) level3 = (Int32)Int32.Parse(settings.GetValue("WagesLevel3"));
                if (settings.HasValue("WagesLevel4")) level4 = (Int32)Int32.Parse(settings.GetValue("WagesLevel4"));
                if (settings.HasValue("WagesLevel5")) level5 = (Int32)Int32.Parse(settings.GetValue("WagesLevel5"));
                if (settings.HasValue("StandbyPercentage")) standbyPct = (Int32)Int32.Parse(settings.GetValue("StandbyPercentage"));
            }
        }

        private void SaveData()
        {
            values = new ConfigNode();
            values.name = "VALUES";
            values.AddValue("OutstandingBills", bills);
            values.AddValue("LoanAmount", loanAmount);
            values.AddValue("LoanPayment", loanPayment);

            values.Save(save_folder + "Financials");
        }

        private void LoadData()
        {
            values = new ConfigNode();
            values = ConfigNode.Load(save_folder + "Financials");
            if (values != null)
            {
                if (values.HasValue("OutstandingBills")) bills = (Double)Double.Parse(values.GetValue("OutstandingBills"));
                if (values.HasValue("LoanAmount")) loanAmount = (Int32)Int32.Parse(values.GetValue("LoanAmount"));
                if (values.HasValue("LoanPayment")) loanPayment = (Int32)Int32.Parse(values.GetValue("LoanPayment"));
            }
        }

        private void ResetToDefault()
        { 
            billing_enabled = true;
            autopayEnabled = false;
            threshold = 50;
            autopayAmount = 0;
            level0 = 10;
            level1 = 20;
            level2 = 40;
            level3 = 80;
            level4 = 140;
            level5 = 200;
            standbyPct = 50;
            yearly = false;
        }
        
        private float PayLoan(float payment)
        {
            loanAmount = loanAmount - payment;
            if (loanAmount == 0)
                loanPayment = 0;
            return payment;
        }

        private void TakeLoan(int loaned, int credit)
        {
            if (loanAmount == 0)
                loanAmount = loaned * ((CalcInterest() + 100) / 100);
        }

        private float CalcInterest()
        {
            int rate = 50;
            if (Reputation.CurrentRep >= 901)
                rate = 5;
            if ((Reputation.CurrentRep > 801) && (Reputation.CurrentRep <= 900))
                rate = 10;
            if ((Reputation.CurrentRep > 701) && (Reputation.CurrentRep <= 800))
                rate = 15;
            if ((Reputation.CurrentRep > 601) && (Reputation.CurrentRep <= 700))
                rate = 20;
            if ((Reputation.CurrentRep > 501) && (Reputation.CurrentRep <= 600))
                rate = 25;
            if ((Reputation.CurrentRep > 401) && (Reputation.CurrentRep <= 500))
                rate = 30;
            if ((Reputation.CurrentRep > 301) && (Reputation.CurrentRep <= 400))
                rate = 35;
            if ((Reputation.CurrentRep > 201) && (Reputation.CurrentRep <= 300))
                rate = 40;
            if ((Reputation.CurrentRep > 101) && (Reputation.CurrentRep <= 200))
                rate = 45;
            if ((Reputation.CurrentRep > 0) && (Reputation.CurrentRep <= 100))
                rate = 50;
            return rate;
        }

        private float CalcLoanPayment(int amount, int interest)
        {
            int t = interest + 100;
            interest = interest / 100;
            float total = amount * interest / 10;
            return total;
        }
    }
}
