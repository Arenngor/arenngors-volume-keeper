using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DiscordKeeper {
public enum SessionRole { Unknown, Notification, Voice }
public static class SessionRouter {
 public static SessionRole Role(string commandLine) {
  if(String.IsNullOrWhiteSpace(commandLine)) return SessionRole.Unknown;
  return commandLine.IndexOf("audio.mojom.AudioService",StringComparison.OrdinalIgnoreCase)>=0
   ? SessionRole.Notification : SessionRole.Voice;
 }
}
public static class DiagnosticReport {
 public static string Create(Settings settings,Row[] notifications,Row[] voices) {
  var text=new StringBuilder();
  text.AppendLine("Arenngor's Volume Keeper "+BuildInfo.Version);
  text.AppendLine("Snapshot (UTC): "+DateTime.UtcNow.ToString("u",CultureInfo.InvariantCulture));
  text.AppendLine("Windows: "+Environment.OSVersion.Version+" | "+(Environment.Is64BitProcess?"64-bit":"32-bit")+" process");
  text.AppendLine("Notification ceiling: "+settings.NotificationCapPercent+"%");
  text.AppendLine("Voice defaults: "+(settings.VoiceDefaultEnabled?"enabled":"disabled")+" ("+settings.VoiceDefaultPercent+"%)");
  text.AppendLine();
  text.AppendLine("Role | PID | output index | volume | last operation");
  Append(text,"Notification",notifications,settings.NotificationCapPercent);
  Append(text,"Voice",voices,settings.VoiceDefaultPercent);
  if(notifications.Length+voices.Length==0) text.AppendLine("No recognized Discord playback sessions. Play a notification or join a call.");
  text.AppendLine();
  text.AppendLine("Mapping: Discord audio service = notifications; other Discord audio processes = voice/media.");
  text.AppendLine("This is the last event snapshot; Apply now refreshes it and may change levels.");
  text.AppendLine("No usernames, executable paths, command lines, conversations or audio are included.");
  return text.ToString();
 }
 static void Append(StringBuilder text,string role,Row[] rows,int target) {
  foreach(Row row in rows) {
   string operation=row.Error!=null?"failed (Core Audio)":row.Changed?"changed":"unchanged";
   float volume=row.Changed?target:row.Percent;
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
