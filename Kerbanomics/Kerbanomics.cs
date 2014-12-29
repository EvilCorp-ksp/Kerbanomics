﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using KSP;

namespace Kerbanomics
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class KerbanomicsMain : MonoBehaviour
    {
        private String save_folder;
        private bool billing_enabled = true;
        private bool autopayEnabled = false;
        private int threshold = 50;
        private int level0 = 10;
        private int level1 = 20;
        private int level2 = 40;
        private int level3 = 80;
        private int level4 = 140;
        public int level5 = 200;
        private float standbyPct = 50;
        private bool yearly = false;
        private bool customInterval = false;
        private bool quarterly = true;
        private int intervalDays = 106;
        string currentInterval = "Quarterly";
        float baseFundsPerDay = 23.474178403755868544600938967136f;
        float fPerDayMult = 2.3239436619718309859154929577465f;
        private int intervalDaysBuffer = 106;

        public double bills = 0;
        private double loanAmount = 0;
        private float loanPayment = 0;
        int addPay = 0;
        int reqAmount = 0;
        float amountFinanced = 0;
        float estPayment = 0;
        int payments = 10;
        double pmt = 0;

        private ConfigNode settings;
        private ConfigNode values;

        double _interval = 2300400;
        public static int _lastUpdate = 0;
        private Rect settingsWindow = new Rect(Screen.height / 8 + 500, Screen.width / 4 , 300, 400);
        private Rect mainWindow = new Rect(Screen.width / 8 + 100, Screen.height / 4, 400, 125); 
        private Rect loanWindow = new Rect(Screen.width / 8 + 500, Screen.height / 4 , 400, 125);
        private Rect payBills = new Rect(Screen.width / 8 + 50, Screen.height / 4, 400, 125);
        public ApplicationLauncherButton button;
        public static KerbanomicsMain Instance;

        void Awake()
        {
            LoadSettings();
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
            //LoadSettings();
            SetInterval();
            UpdateLastUpdate();
            //LoadData();
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
                SetInterval();
                int currentPeriod = (int)Math.Floor(Planetarium.GetUniversalTime() / _interval);
                if (currentPeriod > _lastUpdate && billing_enabled == true)
                {
                    float multiplier = intervalDays;
                    Debug.Log("Last Update=" + _lastUpdate + ", Current Period=" + currentPeriod);
                    _lastUpdate = (currentPeriod);
                    StringBuilder message = new StringBuilder();
                    message.AppendLine("Payroll is processed.");
                    message.AppendLine("Current staff:");
                    foreach (ProtoCrewMember crewMember in HighLogic.CurrentGame.CrewRoster.Crew)
                    {
                        message.Append(crewMember.name);
                        if (!crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Dead) && !crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Missing))
                        {
                            float paycheck = (int)Math.Round(GetWages(crewMember.experienceLevel, crewMember.rosterStatus.ToString()) * multiplier);
                            message.AppendLine(" is " + crewMember.rosterStatus + ". Paycheck = " + paycheck);
                            Debug.Log("Multiplier: " + multiplier);
                            Debug.Log("Wages: " + GetWages(crewMember.experienceLevel, crewMember.rosterStatus.ToString()));
                            bills = (int)Math.Round(bills + paycheck);
                        }
                    }
                    if (loanAmount > 0)
                    {
                        //Funding.Instance.AddFunds(-PayLoan(loanPayment), 0);
                        message.AppendLine("Your loan payment in the amount of " + loanPayment + " is due, please make your payment.");
                    }
                    float externalFunding = CalcFunding();
                    Funding.Instance.AddFunds(+externalFunding, 0);
                    Debug.Log(externalFunding);
                    message.AppendLine("Received Funding - " + externalFunding);
                    message.AppendLine("Amount Due: " + bills.ToString());
                    //SaveData();
                    if (autopayEnabled == true)
                    {
                        message.AppendLine("Autopay enabled, paid out " + AutoPay(bills, Funding.Instance.Funds, threshold).ToString());
                    }
                    MessageSystem.Message m = new MessageSystem.Message(
                        "New Bill Ready",
                        message.ToString(),
                        MessageSystemButton.MessageButtonColor.RED,
                        MessageSystemButton.ButtonIcons.ALERT);
                    MessageSystem.Instance.AddMessage(m);
                }
            }
        }

        private double AutoPay(double due, double available, float pct)
        {
            double payment = 0;
            float mult = pct / 100;
            if (due > ((Double)Math.Ceiling(available * mult)))
            {
                payment = available * mult;
            }
            else if (due <= ((Double)Math.Ceiling(available * mult)))
            {
                payment = due;
            }
            Funding.Instance.AddFunds(-(int)Math.Round(payment), 0);
            bills = bills - (int)Math.Round(payment);
            Debug.Log("Payment: " + payment);
            Debug.Log("AP Pct: " + pct);
            Debug.Log("AP Mult: " + mult);
            return payment;
        }

        private float CalcFunding()
        {
            float f = 0;
            if (quarterly == true)
            {
                f = 2500 + (247.5f * Reputation.CurrentRep);
            }
            if (yearly == true)
            {
                f = 10000 + (990 * Reputation.CurrentRep);
            }
            if (customInterval == true)
            {
                f = (int)Math.Ceiling((baseFundsPerDay + (fPerDayMult * Reputation.CurrentRep)));
            }
            return f;
        }

        private void SetInterval()
        {
            if (customInterval == false)
            {
                if (GameSettings.KERBIN_TIME && quarterly == true)
                    _interval = 2300400;
                else if (!GameSettings.KERBIN_TIME && quarterly == true)
                    _interval = 7884000;
                else if (GameSettings.KERBIN_TIME && yearly == true)
                    _interval = 9201600;
                else if (!GameSettings.KERBIN_TIME && yearly == true)
                    _interval = 31536000;
            }
            else if (customInterval == true)
            {
                _interval = CalcInterval(intervalDays);
            }
        }

        private double GetInterval()
        {
            double interval = _interval;
            return interval;

        }

        private double CalcInterval(int days)
        {
            double seconds = 0;
            seconds = days * 21600;
            return seconds;
        }

        private void UpdateLastUpdate()
        {
            _lastUpdate = (int)Math.Floor(Planetarium.GetUniversalTime() / GetInterval());
        }

        public float GetWages(int level, string status)
        {
            float w = level0;
            switch (level)
            {
                case 0:
                    w = level0;
                    break;
                case 1:
                    w = level1;
                    break;
                case 2:
                    w = level2;
                    break;
                case 3:
                    w = level3;
                    break;
                case 4:
                    w = level4;
                    break;
                case 5:
                    w = level5;
                    break;
                default:
                    w = 10;
                    break;
            }
            if (status == "Available")
            {
                float pBuf = w / 100;
                w = pBuf * standbyPct;
            }
            return w;
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
            //if (loanAmount > 0)
            //{
            //    loanWindow = GUILayout.Window(87441, loanWindow, LoanWindLayoutDeny, "Loans");
            //}
            //else
            //{
                loanWindow = GUILayout.Window(56489, loanWindow, LoanWindLayoutApprove, "Loans");
            //}
        }

        private double CalculateThreshold()
        {
            double maxPayment = 0;
            float mult = threshold / 100;
            maxPayment = Funding.Instance.Funds * threshold;
            return maxPayment;
        }

        private void DrawInvoiceWindow()
        {
            payBills = GUILayout.Window(81365, payBills, InvoiceWindow, "Pending Bills");
        }

        private void OnWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Outstanding Bills: " + bills.ToString());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Pay Bills"))
            {
                RenderingManager.AddToPostDrawQueue(0, DrawInvoiceWindow);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Loan Balance: " + Double.Parse(loanAmount.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Next Loan Payment: " + loanPayment.ToString());
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

        private void CycleInterval()
        {
            if (customInterval == true)
            {
                yearly = false;
                quarterly = true;
                customInterval = false;
                currentInterval = "Quarterly";
                Debug.Log("Changed from Custom to Quarterly");
            }
            else if (yearly == true)
            {
                yearly = false;
                customInterval = true;
                quarterly = false;
                currentInterval = "Custom";
                Debug.Log("Changed from Yearly to Custom");
            }
            else if (quarterly == true)
            {
                customInterval = false;
                quarterly = false;
                yearly = true;
                currentInterval = "Yearly";
                Debug.Log("Changed from Quarterly to Yearly");
            }
            SetInterval();
        }

        private void SettingsWind(int windowId)
        {
            GUILayout.BeginHorizontal();
            billing_enabled = GUILayout.Toggle(billing_enabled, "Enabled");
            GUILayout.EndHorizontal();
            if (billing_enabled == true)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Interval: ");
                if (customInterval == true)
                {
                    if (GUILayout.Button(currentInterval, GUILayout.Width(75)))
                    {
                        CycleInterval();
                    }
                    GUILayout.FlexibleSpace();
                    intervalDaysBuffer = Convert.ToInt32(GUILayout.TextField(intervalDaysBuffer.ToString(), 4, GUILayout.Width(50)));
                    GUILayout.Label(" days");
                }
                if (yearly == true)
                {
                    if (GUILayout.Button(currentInterval, GUILayout.Width(75)))
                    {
                        CycleInterval();
                    }
                }
                if (quarterly == true)
                {
                    if (GUILayout.Button(currentInterval, GUILayout.Width(75)))
                    {
                        CycleInterval();
                    }
                }
                //if (billing_enabled == false)
                //{
                //    if (GUILayout.Button(currentInterval, GUILayout.Width(75)))
                //    {
                //        CycleInterval();
                //    }
                //}
                //yearly = GUILayout.Toggle(yearly, "Yearly Billing");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            autopayEnabled = GUILayout.Toggle(autopayEnabled, "Enable Autopay");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Maximum % of funds to autopay: ");
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
                SetInterval();
                UpdateLastUpdate();
                intervalDays = intervalDaysBuffer;
            }
            if (GUILayout.Button("Reset to Default", GUILayout.ExpandWidth(true)))
            {
                ResetToDefault();
                SaveSettings();
                SetInterval();
                UpdateLastUpdate();
            }
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(true)))
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawSettings);
                SetInterval();
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
            GUILayout.Label("Total amount financed: " + Double.Parse(amountFinanced.ToString()));
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
            }
            if (amountFinanced != 0)
            {
                if (GUILayout.Button("Accept"))
                {
                    Funding.Instance.AddFunds(reqAmount, 0);
                    loanAmount = loanAmount + amountFinanced;
                    loanPayment = loanPayment + (amountFinanced / payments);
                    RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
                }
            }
            if (GUILayout.Button("Close"))
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (loanAmount > 0)
            {

                GUILayout.BeginHorizontal();
                GUILayout.Label("Make a Payment: "); 
                GUILayout.FlexibleSpace();
                addPay = Convert.ToInt32(GUILayout.TextField(addPay.ToString(), 7, GUILayout.Width(75)));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Total Financed: " + loanAmount);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pay", GUILayout.ExpandWidth(true)))
                {
                    Funding.Instance.AddFunds(-addPay, 0);
                    PayLoan(addPay);
                    RenderingManager.RemoveFromPostDrawQueue(0, DrawLoanWindow);
                }
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow();
        }

        private void InvoiceWindow(int windowId)
        {
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Amount Due: " + bills);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Payment: ");
            GUILayout.FlexibleSpace();
            pmt = Convert.ToDouble(GUILayout.TextField(pmt.ToString(), 7, GUILayout.Width(75)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Pay", GUILayout.Width(75)))
            {
                Funding.Instance.AddFunds(-pmt, 0);
                bills = bills - pmt;
                //SaveData();
                RenderingManager.RemoveFromPostDrawQueue(0, DrawInvoiceWindow);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(75)))
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawInvoiceWindow);
            }
            GUILayout.FlexibleSpace();
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

        public void SaveSettings()
        {
            settings = new ConfigNode();
            settings.name = "SETTINGS";
            settings.AddValue("Enabled", billing_enabled);
            settings.AddValue("Yearly", yearly);
            settings.AddValue("Threshold", threshold);
            settings.AddValue("Autopay", autopayEnabled);
            settings.AddValue("WagesLevel0", level0);
            settings.AddValue("WagesLevel1", level1);
            settings.AddValue("WagesLevel2", level2);
            settings.AddValue("WagesLevel3", level3);
            settings.AddValue("WagesLevel4", level4);
            settings.AddValue("WagesLevel5", level5);
            settings.AddValue("StandbyPercentage", standbyPct);

            settings.Save(save_folder + "Settings.cfg");
        }

        public void LoadSettings()
        {
            settings = new ConfigNode();
            settings = ConfigNode.Load(save_folder + "Settings.cfg");
            if (settings != null)
            {
                if (settings.HasValue("Enabled")) billing_enabled = Boolean.Parse(settings.GetValue("Enabled"));
                if (settings.HasValue("Yearly")) yearly = Boolean.Parse(settings.GetValue("Yearly"));
                if (settings.HasValue("Threshold")) threshold = (Int32)Int32.Parse(settings.GetValue("Threshold"));
                if (settings.HasValue("Autopay")) autopayEnabled = Boolean.Parse(settings.GetValue("Autopay"));
                if (settings.HasValue("WagesLevel0")) level0 = (Int32)Int32.Parse(settings.GetValue("WagesLevel0"));
                if (settings.HasValue("WagesLevel1")) level1 = (Int32)Int32.Parse(settings.GetValue("WagesLevel1"));
                if (settings.HasValue("WagesLevel2")) level2 = (Int32)Int32.Parse(settings.GetValue("WagesLevel2"));
                if (settings.HasValue("WagesLevel3")) level3 = (Int32)Int32.Parse(settings.GetValue("WagesLevel3"));
                if (settings.HasValue("WagesLevel4")) level4 = (Int32)Int32.Parse(settings.GetValue("WagesLevel4"));
                if (settings.HasValue("WagesLevel5")) level5 = (Int32)Int32.Parse(settings.GetValue("WagesLevel5"));
                if (settings.HasValue("StandbyPercentage")) standbyPct = (Int32)Int32.Parse(settings.GetValue("StandbyPercentage"));
            }
        }

        public void SaveData()
        {
            values = new ConfigNode();
            values.name = "VALUES";
            values.AddValue("OutstandingBills", bills);
            values.AddValue("LoanAmount", loanAmount);
            values.AddValue("LoanPayment", loanPayment);

            values.Save(save_folder + "Financials");
        }

        public void LoadData()
        {
            values = new ConfigNode();
            values = ConfigNode.Load(save_folder + "Financials");
            if (values != null)
            {
                if (values.HasValue("OutstandingBills")) bills = (Double)Double.Parse(values.GetValue("OutstandingBills"));
                if (values.HasValue("LoanAmount")) loanAmount = (Double)Double.Parse(values.GetValue("LoanAmount"));
                if (values.HasValue("LoanPayment")) loanPayment = (Int32)Int32.Parse(values.GetValue("LoanPayment"));
            }
        }

        private void ResetToDefault()
        { 
            billing_enabled = true;
            autopayEnabled = false;
            threshold = 50;
            level0 = 10;
            level1 = 20;
            level2 = 40;
            level3 = 80;
            level4 = 140;
            level5 = 200;
            standbyPct = 50;
            yearly = false;
            quarterly = true;
            customInterval = false;
            intervalDays = 106;
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
