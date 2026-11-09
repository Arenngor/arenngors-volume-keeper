using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DiscordKeeper {
public sealed class Settings {
 public int NotificationCapPercent = 5;
 public int VoiceDefaultPercent = 30;
 public bool VoiceDefaultEnabled = false;
 public bool SystemSoundsCapEnabled = false;
 public int SystemSoundsCapPercent = 25;
 public Settings Copy() { return (Settings)MemberwiseClone(); }
 public void Validate() {
  if(NotificationCapPercent<0 || NotificationCapPercent>100 || SystemSoundsCapPercent<0 || SystemSoundsCapPercent>100 || VoiceDefaultPercent<0 || VoiceDefaultPercent>100)
   throw new InvalidDataException("Volumes must be between 0 and 100.");
 }
 public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiscordVolumeKeeper");
 public static readonly string FilePath = Path.Combine(Folder,"settings.json");
 public static Settings Load() {
  string path = File.Exists(FilePath) ? FilePath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");
  if(!File.Exists(path)) return new Settings();
  return FromJson(File.ReadAllText(path));
 }
 public static Settings FromJson(string json) {
  var serializer = new JavaScriptSerializer();
  var value = serializer.Deserialize<Settings>(json);
  if(value==null) throw new InvalidDataException("Settings file is empty.");
  value.Validate(); return value;
 }
 public void Save() {
  Validate(); Directory.CreateDirectory(Folder);
  string temp=FilePath+".tmp"; File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));
  if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
 }
}
public static class Startup {
 const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
 const string Name = "DiscordVolumeKeeper";
 public static bool Enabled { get { try { using(var k=Registry.CurrentUser.OpenSubKey(Key)) {
  return k!=null && String.Equals(k.GetValue(Name) as string,Command,StringComparison.OrdinalIgnoreCase);
 } } catch(System.Security.SecurityException) { return false; } catch(UnauthorizedAccessException) { return false; } } }
 static string Command { get { return "\""+Application.ExecutablePath+"\" --tray"; } }
 public static void Set(bool enabled) {
  using(var k=Registry.CurrentUser.CreateSubKey(Key)) {
   if(enabled) k.SetValue(Name,Command); else k.DeleteValue(Name,false);
  }
  // Migrate only the exact shortcut installed by our earlier script.
  if(enabled) {
   string old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Discord Volume Keeper.lnk");
   if(File.Exists(old)) File.Delete(old);
  }
 }
}
public sealed class Engine : IDisposable {
 readonly object gate = new object();
 readonly Thread worker;
 Settings settings;
 Watcher watcher;
 volatile bool stop, paused, requested=true;
 bool resetVoice;
 string lastVoiceApply="";
 string status="Starting audio listener...";
 string diagnostics="Waiting for the first audio event.";
 string diagnosticStatus="Starting audio listener.";
 public string Diagnostics { get { lock(gate) return diagnostics; } }
 public string DiagnosticStatus { get { lock(gate) return diagnosticStatus; } }
 public string Status { get { lock(gate) return status; } }
 public bool Paused { get { return paused; } }
 public Engine(Settings value) {
  settings=value.Copy(); diagnostics=DiagnosticReport.Create(settings,new Row[0]);
  worker=new Thread(Run); worker.IsBackground=true;
  worker.SetApartmentState(ApartmentState.MTA); worker.Start();
 }
 void SetStatus(string text) { lock(gate) { status=text; diagnosticStatus=text; } }
 public void Configure(Settings value, bool applyVoice) {
  lock(gate) {
   if(value.VoiceDefaultEnabled && (applyVoice || !settings.VoiceDefaultEnabled)) resetVoice=true;
   settings=value.Copy();
  }
  Signal();
 }
 public void Pause(bool value) { paused=value; Signal(); }
 void Signal() { requested=true; lock(gate) { if(watcher!=null) watcher.Wake(); } }
 void Process(Watcher listener) {
  if(paused) { SetStatus("Paused - volumes are left alone."); return; }
  Settings current; bool explicitVoiceReset;
  lock(gate) { current=settings.Copy(); explicitVoiceReset=resetVoice; resetVoice=false; }
  if(explicitVoiceReset) Audio.ResetVoice();
  if(!current.VoiceDefaultEnabled) lastVoiceApply="";
  var notifications=new Dictionary<uint,float>();
  var voices=new Dictionary<uint,float>();
  int processes=0;
  using(var search=new ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='Discord.exe' OR Name='DiscordPTB.exe' OR Name='DiscordCanary.exe'"))
  using(var results=search.Get()) {
   foreach(ManagementObject item in results) using(item) {
    processes++; string line=item["CommandLine"] as string;
    if(String.IsNullOrEmpty(line)) continue;
    uint pid=Convert.ToUInt32(item["ProcessId"]);
    if(SessionRouter.IsNotificationCandidate(line))
     notifications[pid]=current.NotificationCapPercent/100f;
    else if(current.VoiceDefaultEnabled) voices[pid]=current.VoiceDefaultPercent/100f;
   }
  }
  float? systemCap=current.SystemSoundsCapEnabled ? (float?)(current.SystemSoundsCapPercent/100f) : null;
  listener.Observe(notifications,systemCap);
  Row[] rows=Audio.Scan(notifications,systemCap,voices);
  lock(gate) diagnostics=DiagnosticReport.Create(current,rows);
  int skipped=Array.FindAll(rows,row=>row.SkipReason!=null).Length;
  int system=Array.FindAll(rows,row=>row.IsSystemSounds).Length;
  int discord=Array.FindAll(rows,row=>!row.IsSystemSounds && !row.IsVoice && row.PID!=0).Length;
  int voice=Array.FindAll(rows,row=>row.IsVoice && row.SkipReason==null && row.Error==null).Length;
  if(explicitVoiceReset && current.VoiceDefaultEnabled)
   lastVoiceApply=voice==0 ? "No eligible voice session. Join a call and try Apply now." : "Voice default applied to "+voice+" session(s).";
  SetStatus((processes==0 ? "Waiting for Discord." : "Discord notification candidates: "+discord+".")+
   (systemCap.HasValue ? " System Sounds sessions: "+system+"." : "")+
   (skipped>0 ? "\nSkipped "+skipped+" session(s) or outputs. See Diagnostics." : "")+
   (lastVoiceApply.Length>0 ? "\n"+lastVoiceApply : ""));
 }
 void Run() {
  while(!stop) {
   Watcher current=null;
   try {
    current=new Watcher(); lock(gate) watcher=current;
    requested=true;
    while(!stop) {
     bool audioEvent=current.Wait(250);
     if(stop) break;
     if(!audioEvent && !requested) continue;
     requested=false; current.Refresh(); Process(current);
    }
   } catch(Exception ex) {
    lock(gate) {
     status="Audio listener unavailable: "+ex.Message+" Retrying...";
     diagnosticStatus="Audio listener unavailable ("+ex.GetType().Name+"). Retrying.";
    }
    try {
     Directory.CreateDirectory(Settings.Folder); string log=Path.Combine(Settings.Folder,"errors.log");
     if(File.Exists(log) && new FileInfo(log).Length>262144) {
      string previous=Path.Combine(Settings.Folder,"errors.previous.log");
      if(File.Exists(previous)) File.Delete(previous); File.Move(log,previous);
     }
     File.AppendAllText(log,DateTime.Now+" "+ex+Environment.NewLine);
    } catch { }
    for(int i=0;i<20 && !stop;i++) Thread.Sleep(250);
   } finally {
    lock(gate) watcher=null;
    if(current!=null) { try { current.Dispose(); } catch { } }
   }
  }
 }
 public void Dispose() { stop=true; Signal(); worker.Join(2000); }
}
public sealed class SettingsWindow : Form {
 readonly Engine engine;
 readonly NumericUpDown notifications, systemSounds, voice;
 readonly CheckBox startup, systemSoundsEnabled, voiceEnabled;
 readonly Label status;
 readonly Button pause;
 readonly System.Windows.Forms.Timer timer;
 bool exiting;
 static readonly Color Background=Color.FromArgb(17,22,34), Card=Color.FromArgb(27,34,49), Ink=Color.FromArgb(235,240,249), Muted=Color.FromArgb(166,179,201), Accent=Color.FromArgb(139,158,255);
 public SettingsWindow(Engine value, Settings config) {
  engine=value;
  Text="Arenngor's Volume Keeper - "+BuildInfo.Version; ClientSize=new Size(570,681); BackColor=Background; ForeColor=Ink;
  Font=new Font("Segoe UI",10); FormBorderStyle=FormBorderStyle.FixedSingle;
  MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi;
  Icon=Program.AppIcon;
  AddLabel(this,"VOLUME LIMITS",26,20,500,20,9,Accent,FontStyle.Bold);
  AddLabel(this,"Arenngor's Volume Keeper",24,44,520,40,21,Ink,FontStyle.Bold);
  AddLabel(this,"Discord notifications, optional voice defaults, and Windows sounds.",26,90,520,25,10,Muted,FontStyle.Regular);
  Panel n=MakeCard(132);
  AddLabel(n,"Notification ceiling",18,14,350,25,12,Ink,FontStyle.Bold);
  AddLabel(n,"Cap Discord pings; keep lower levels.",18,48,350,26,10,Muted,FontStyle.Regular);
  notifications=Number(n,config.NotificationCapPercent);
  Panel systemCard=MakeCard(242);
  systemSoundsEnabled=new CheckBox { Text="Cap Windows System Sounds",Location=new Point(18,13),Size=new Size(350,26),ForeColor=Ink,Checked=config.SystemSoundsCapEnabled };
  systemCard.Controls.Add(systemSoundsEnabled);
  AddLabel(systemCard,"Applies to the System Sounds mixer entry.",18,48,365,26,10,Muted,FontStyle.Regular);
  systemSounds=Number(systemCard,config.SystemSoundsCapPercent);
  systemSounds.Enabled=systemSoundsEnabled.Checked;
  systemSoundsEnabled.CheckedChanged+=delegate { systemSounds.Enabled=systemSoundsEnabled.Checked; };
  Panel voiceCard=MakeCard(352);
  voiceEnabled=new CheckBox { Text="Set a default Discord voice volume",Location=new Point(18,13),Size=new Size(355,26),ForeColor=Ink,Checked=config.VoiceDefaultEnabled };
  voiceCard.Controls.Add(voiceEnabled);
  AddLabel(voiceCard,"Set once per session; adjust freely afterward.",18,48,365,26,10,Muted,FontStyle.Regular);
  voice=Number(voiceCard,config.VoiceDefaultPercent);
  voice.Enabled=voiceEnabled.Checked;
  voiceEnabled.CheckedChanged+=delegate { voice.Enabled=voiceEnabled.Checked; };
  startup=new CheckBox { Text="Start with Windows",Location=new Point(28,468),Size=new Size(250,28),Checked=Startup.Enabled,ForeColor=Ink };
  Controls.Add(startup);
  Button save=MakeButton("Save settings",26,512,160,true); save.Click+=delegate { Save(false); };
  Button apply=MakeButton("Apply now",198,512,140,false); apply.Click+=delegate { Save(true); };
  pause=MakeButton("Pause",350,512,100,false); pause.Click+=delegate { TogglePause(); };
  status=AddLabel(this,"Starting...",26,558,520,55,9,Muted,FontStyle.Regular);
  Button diagnosticButton=MakeButton("Diagnostics",26,624,135,false);
  diagnosticButton.Click+=delegate { ShowDiagnostics(); };
  Button about=MakeButton("About",173,624,95,false); about.Click+=delegate { ShowAbout(); };
  AddLabel(this,"Close to tray. Right-click the tray icon to exit.",280,632,270,25,8,Muted,FontStyle.Regular);
  timer=new System.Windows.Forms.Timer { Interval=600 }; timer.Tick+=delegate { status.Text=engine.Status; pause.Text=engine.Paused?"Resume":"Pause"; }; timer.Start();
  FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(!exiting && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); } };
 }
 Panel MakeCard(int y) { var p=new Panel { Location=new Point(26,y),Size=new Size(518,94),BackColor=Card }; Controls.Add(p); return p; }
 static Label AddLabel(Control parent,string text,int x,int y,int width,int height,float size,Color color,FontStyle style) {
  var l=new Label { Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=color,Font=new Font("Segoe UI",size,style),BackColor=Color.Transparent }; parent.Controls.Add(l); return l;
 }
 NumericUpDown Number(Control parent,int initial) {
  var n=new NumericUpDown { Minimum=0,Maximum=100,Value=initial,Location=new Point(397,26),Size=new Size(72,34),Font=new Font("Segoe UI",14,FontStyle.Bold),BackColor=Background,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle };
  parent.Controls.Add(n); AddLabel(parent,"%",475,32,25,26,12,Muted,FontStyle.Regular); return n;
 }
 Button MakeButton(string text,int x,int y,int width,bool primary) {
  var b=new Button { Text=text,Location=new Point(x,y),Size=new Size(width,36),FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Card,ForeColor=primary?Background:Ink,Cursor=Cursors.Hand };
  b.FlatAppearance.BorderSize=0; Controls.Add(b); return b;
 }
 void Save(bool applyVoice) {
  try {
   var next=new Settings { NotificationCapPercent=(int)notifications.Value, SystemSoundsCapEnabled=systemSoundsEnabled.Checked, SystemSoundsCapPercent=(int)systemSounds.Value, VoiceDefaultEnabled=voiceEnabled.Checked, VoiceDefaultPercent=(int)voice.Value };
   next.Save(); engine.Configure(next,applyVoice);
   Startup.Set(startup.Checked);
   status.Text=engine.Paused?"Saved. Resume to apply the ceilings.":"Saved. Checking audio sessions...";
  } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Could not save",MessageBoxButtons.OK,MessageBoxIcon.Error); }
 }
 public void TogglePause() { engine.Pause(!engine.Paused); }
 public void ShowDiagnostics() { using(var dialog=new DiagnosticsWindow(engine)) dialog.ShowDialog(this); }
 public void ShowAbout() {
  MessageBox.Show(this,"Arenngor's Volume Keeper\nVersion "+BuildInfo.Version+
   "\n\nCaps Discord notifications and optionally Windows System Sounds. Discord voice defaults are optional. Windows may reuse the ping level for voice after a restart."+
   "\n\nMIT License - Copyright 2026 Arenngor\nIndependent utility; not affiliated with Discord."+
   "\n\nFor bug reports: copy Diagnostics, describe what happened, and include your Discord version."+
   "\n\nPortable app. For installation or updates, exit and run Install.cmd from a release folder.",
   "About Arenngor's Volume Keeper",MessageBoxButtons.OK,MessageBoxIcon.Information);
 }
 public void Shutdown() { exiting=true; timer.Stop(); timer.Dispose(); Close(); }
}
public sealed class TrayApp : ApplicationContext {
 readonly NotifyIcon tray;
 readonly SettingsWindow window;
 readonly Engine engine;
 public TrayApp(Settings settings,bool hidden) {
  engine=new Engine(settings); window=new SettingsWindow(engine,settings);
  var menu=new ContextMenuStrip();
  menu.Items.Add("Settings",null,delegate { Open(); });
  menu.Items.Add("Diagnostics",null,delegate { window.ShowDiagnostics(); });
  menu.Items.Add("About",null,delegate { window.ShowAbout(); });
  var pause=new ToolStripMenuItem("Pause"); pause.Click+=delegate { window.TogglePause(); }; menu.Items.Add(pause);
  menu.Opening+=delegate { pause.Text=engine.Paused?"Resume":"Pause"; };
  menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit",null,delegate { ExitThread(); });
  tray=new NotifyIcon { Icon=Program.AppIcon, Text="Arenngor's Volume Keeper",ContextMenuStrip=menu,Visible=true };
  tray.DoubleClick+=delegate { Open(); };
  if(!hidden) window.Show();
 }
 void Open() { window.Show(); window.WindowState=FormWindowState.Normal; window.Activate(); }
 protected override void ExitThreadCore() {
  tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); window.Shutdown(); window.Dispose(); engine.Dispose(); base.ExitThreadCore();
 }
}
public static class Program {
 public static readonly Icon AppIcon=Icon.ExtractAssociatedIcon(typeof(Program).Assembly.Location) ?? SystemIcons.Information;
 [STAThread] public static void Main(string[] args) {
  using(var mutex=new Mutex(false,@"Local\DiscordVolumeKeeper")) {
   bool owned=false;
   try {
    try { owned=mutex.WaitOne(0); } catch(AbandonedMutexException) { owned=true; }
    if(!owned) { MessageBox.Show("Volume Keeper is already running. Use its tray icon, or stop the old PowerShell keeper before opening this app.","Already running"); return; }
    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
    Settings settings;
    try { settings=Settings.Load(); } catch(Exception ex) { MessageBox.Show("Could not load settings; using a 5% notification ceiling.\n"+ex.Message,"Volume Keeper"); settings=new Settings(); }
    Application.Run(new TrayApp(settings,Array.IndexOf(args,"--tray")>=0));
   } catch(Exception ex) { MessageBox.Show(ex.Message,"Arenngor's Volume Keeper",MessageBoxButtons.OK,MessageBoxIcon.Error); }
   finally { if(owned) mutex.ReleaseMutex(); }
  }
 }
}
}
