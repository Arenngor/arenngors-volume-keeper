using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DiscordKeeper {
public static class SessionRouter {
 public static bool IsNotificationCandidate(string commandLine) {
  return !String.IsNullOrWhiteSpace(commandLine) &&
   commandLine.IndexOf("audio.mojom.AudioService",StringComparison.OrdinalIgnoreCase)>=0;
 }
}
public static class DiagnosticReport {
 public static string Create(Settings settings,Row[] rows) {
  var text=new StringBuilder();
  text.AppendLine("Arenngor's Volume Keeper "+BuildInfo.Version);
  text.AppendLine("Snapshot (UTC): "+DateTime.UtcNow.ToString("u",CultureInfo.InvariantCulture));
  text.AppendLine("Windows: "+Environment.OSVersion.Version+" | "+(Environment.Is64BitProcess?"64-bit":"32-bit")+" process");
  text.AppendLine("Notification ceiling: "+settings.NotificationCapPercent+"%");
  text.AppendLine("System Sounds ceiling: "+(settings.SystemSoundsCapEnabled ? settings.SystemSoundsCapPercent+"%" : "off"));
  text.AppendLine("Voice default: "+(settings.VoiceDefaultEnabled ? settings.VoiceDefaultPercent+"%" : "off"));
  text.AppendLine();
  text.AppendLine("Role | PID | output index | volume | last operation");
  Append(text,rows);
  if(rows.Length==0) text.AppendLine("No matching sessions yet. Play a Discord notification or a Windows system sound.");
  text.AppendLine();
  text.AppendLine("Discord audio-service sessions are notification candidates. Windows may reuse their saved levels on restart.");
  text.AppendLine("This is the last event snapshot; Apply now refreshes it and may change levels.");
  text.AppendLine("No usernames, executable paths, command lines, conversations or audio are included.");
  return text.ToString();
 }
 static void Append(StringBuilder text,Row[] rows) {
  foreach(Row row in rows) {
   string operation=row.Error!=null?"failed (Core Audio)":row.SkipReason!=null?"skipped: "+row.SkipReason:row.Changed?"changed":"unchanged";
   string role=row.IsSystemSounds?"System Sounds":row.IsVoice?"Voice":row.PID==0?"Output":"Notification";
   float volume=row.Percent;
   text.AppendLine(String.Format(CultureInfo.InvariantCulture,"{0} | {1} | {2} | {3:0.#}% | {4}",role,row.PID,row.Device,volume,operation));
  }
 }
}
public sealed class DiagnosticsWindow : Form {
 readonly Engine engine;
 readonly TextBox report;
 public DiagnosticsWindow(Engine value) {
  engine=value; Text="Diagnostics - Arenngor's Volume Keeper"; Size=new Size(760,500);
  StartPosition=FormStartPosition.CenterParent; MinimumSize=new Size(560,350);
  report=new TextBox { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font("Consolas",10) };
  var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=44,Padding=new Padding(5) };
  var refresh=new Button { Text="Refresh view",AutoSize=true }; refresh.Click+=delegate { RefreshText(); };
  var copy=new Button { Text="Copy report",AutoSize=true }; copy.Click+=delegate {
   try { Clipboard.SetText(report.Text); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Could not copy"); }
  };
  var save=new Button { Text="Save report...",AutoSize=true }; save.Click+=delegate {
   using(var dialog=new SaveFileDialog { Filter="Text files|*.txt",FileName="VolumeKeeper-diagnostics.txt" }) {
    if(dialog.ShowDialog(this)!=DialogResult.OK) return;
    try { File.WriteAllText(dialog.FileName,report.Text); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Could not save"); }
   }
  };
  buttons.Controls.AddRange(new Control[] { refresh,copy,save }); Controls.Add(report); Controls.Add(buttons); RefreshText();
 }
 void RefreshText() {
  report.Text="Status: "+engine.DiagnosticStatus+Environment.NewLine+Environment.NewLine+engine.Diagnostics;
  report.SelectionStart=0; report.SelectionLength=0;
 }
}
}
