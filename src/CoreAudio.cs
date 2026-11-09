using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
namespace DiscordKeeper {
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator {}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevices {
 void EnumAudioEndpoints(int flow, uint state, out ICollection devices);
 void GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
 void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
 void RegisterEndpointNotificationCallback(IDeviceEvents callback);
 void UnregisterEndpointNotificationCallback(IDeviceEvents callback);
}
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ICollection {
 void GetCount(out uint count); void Item(uint index, out IDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
 void Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
 void OpenPropertyStore(uint access, out IntPtr store);
 void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IManager {
 void GetAudioSessionControl(ref Guid sessionGuid, uint flags, out IControl control);
 void GetSimpleAudioVolume(ref Guid sessionGuid, uint flags, out IVolume volume);
 void GetSessionEnumerator(out ISessions sessions);
 void RegisterSessionNotification(ISessionEvents callback);
 void UnregisterSessionNotification(ISessionEvents callback);
}
[ComVisible(true), Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISessionEvents { [PreserveSig] int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)] object session); }
[ComVisible(true), Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IVolumeEvents {
 [PreserveSig] int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
 [PreserveSig] int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
 [PreserveSig] int OnSimpleVolumeChanged(float volume, [MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
 [PreserveSig] int OnChannelVolumeChanged(uint count, IntPtr volumes, uint channel, ref Guid context);
 [PreserveSig] int OnGroupingParamChanged(ref Guid grouping, ref Guid context);
 [PreserveSig] int OnStateChanged(int state);
 [PreserveSig] int OnSessionDisconnected(int reason);
}
[StructLayout(LayoutKind.Sequential)] public struct PropertyKey { public Guid Format; public uint Id; }
[ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDeviceEvents {
 [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
 [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
 [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
 [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
 [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class Watcher : ISessionEvents, IDeviceEvents, IVolumeEvents, IDisposable {
 readonly AutoResetEvent signal = new AutoResetEvent(false);
 readonly Dictionary<string,object> managers = new Dictionary<string,object>();
 readonly Dictionary<string,IControl> watched = new Dictionary<string,IControl>();
 object root;
 volatile bool disposed;
 public Watcher() {
  if(Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
   throw new InvalidOperationException("Run PowerShell with -Mta (use Start.cmd).");
  try { root = new Enumerator(); ((IDevices)root).RegisterEndpointNotificationCallback(this); Refresh(); }
  catch { Dispose(); throw; }
 }
 public void Wake() { Notify(); }
 public bool Wait(int milliseconds) { return signal.WaitOne(milliseconds); }
 void Notify() { if(!disposed) { try { signal.Set(); } catch(ObjectDisposedException) {} } }
 public int OnSessionCreated(object session) { Notify(); return 0; }
 public int OnDeviceStateChanged(string id, uint state) { Notify(); return 0; }
 public int OnDeviceAdded(string id) { Notify(); return 0; }
 public int OnDeviceRemoved(string id) { Notify(); return 0; }
 public int OnDefaultDeviceChanged(int flow, int role, string id) { Notify(); return 0; }
 public int OnPropertyValueChanged(string id, PropertyKey key) { return 0; }
 public int OnDisplayNameChanged(string name, ref Guid context) { return 0; }
 public int OnIconPathChanged(string path, ref Guid context) { return 0; }
 public int OnSimpleVolumeChanged(float volume, bool mute, ref Guid context) { Notify(); return 0; }
 public int OnChannelVolumeChanged(uint count, IntPtr volumes, uint channel, ref Guid context) { return 0; }
 public int OnGroupingParamChanged(ref Guid grouping, ref Guid context) { Notify(); return 0; }
 public int OnStateChanged(int state) { Notify(); return 0; }
 public int OnSessionDisconnected(int reason) { Notify(); return 0; }
 public void Observe(Dictionary<uint,float> targets, float? systemCap) {
  foreach(string key in new List<string>(watched.Keys)) {
   int state=2; try { watched[key].GetState(out state); } catch(COMException) {}
   if(state!=2) continue;
   try { watched[key].UnregisterAudioSessionNotification(this); } catch(COMException) {}
   Audio.Release(watched[key]); watched.Remove(key);
  }
  foreach(object manager in managers.Values) {
   ISessions sessions=null;
   try {
    ((IManager)manager).GetSessionEnumerator(out sessions); int count; sessions.GetCount(out count);
    for(int i=0;i<count;i++) {
     IControl control=null;
     try {
      sessions.GetSession(i,out control); uint pid;
      int owner=control.GetProcessId(out pid);
      var identity=new SessionIdentity { PID=pid, OwnerStatus=owner, IsSystemSounds=Audio.IsSystemSounds(control) };
      float cap;
      if(!NotificationPolicy.TryGetCap(identity,targets,systemCap,out cap) || (!identity.IsSystemSounds && owner!=0)) continue;
      string key; control.GetSessionInstanceIdentifier(out key);
      if(watched.ContainsKey(key)) continue;
      control.RegisterAudioSessionNotification(this); watched.Add(key,control); control=null;
     } catch(COMException) {} finally { Audio.Release(control); }
    }
   } catch(COMException) {} finally { Audio.Release(sessions); }
  }
 }
 public void Refresh() {
  ICollection devices = null; var active = new HashSet<string>();
  try {
   ((IDevices)root).EnumAudioEndpoints(0,1,out devices); uint n; devices.GetCount(out n);
   for(uint i=0;i<n;i++) {
    IDevice device=null; object manager=null; ISessions sessions=null; bool registered=false;
    try {
     devices.Item(i,out device); string id; device.GetId(out id); active.Add(id);
     if(managers.ContainsKey(id)) continue;
     Guid iid=typeof(IManager).GUID; device.Activate(ref iid,23,IntPtr.Zero,out manager);
     ((IManager)manager).RegisterSessionNotification(this); registered=true;
     ((IManager)manager).GetSessionEnumerator(out sessions); int count; sessions.GetCount(out count);
     managers.Add(id,manager); manager=null;
    } catch(COMException) {} finally {
     if(manager!=null && registered) { try { ((IManager)manager).UnregisterSessionNotification(this); } catch(COMException) {} }
     Audio.Release(sessions); Audio.Release(manager); Audio.Release(device);
    }
   }
   foreach(string id in new List<string>(managers.Keys)) {
    if(active.Contains(id)) continue;
    try { ((IManager)managers[id]).UnregisterSessionNotification(this); } catch(COMException) {}
    Audio.Release(managers[id]); managers.Remove(id);
   }
  } finally { Audio.Release(devices); }
 }
 public void Dispose() {
  if(disposed) return; disposed=true;
  if(root!=null) { try { ((IDevices)root).UnregisterEndpointNotificationCallback(this); } catch(COMException) {} }
  foreach(object manager in managers.Values) {
   try { ((IManager)manager).UnregisterSessionNotification(this); } catch(COMException) {}
   Audio.Release(manager);
  }
  foreach(IControl control in watched.Values) {
   try { control.UnregisterAudioSessionNotification(this); } catch(COMException) {}
   Audio.Release(control);
  }
  watched.Clear();
  managers.Clear(); Audio.Release(root); root=null; signal.Dispose();
 }
}
[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessions {
 void GetCount(out int count); void GetSession(int index, out IControl control);
}
[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IControl {
 void GetState(out int state);
 void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
 void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
 void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
 void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
 void GetGroupingParam(out Guid grouping); void SetGroupingParam(ref Guid grouping, ref Guid context);
 void RegisterAudioSessionNotification(IVolumeEvents events); void UnregisterAudioSessionNotification(IVolumeEvents events);
 void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
 void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
 [PreserveSig] int GetProcessId(out uint pid);
 [PreserveSig] int IsSystemSoundsSession();
}
[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume {
 void SetMasterVolume(float volume, ref Guid context); void GetMasterVolume(out float volume);
 void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
public class Row { public bool IsSystemSounds; public bool IsVoice; public uint PID; public int Device; public float Percent; public bool Changed; public string Error; public string SkipReason; }
public sealed class SessionIdentity {
 public uint PID;
 public int OwnerStatus;
 public bool IsSystemSounds;
 public Guid Group;
 public string Identifier;
}
public static class NotificationPolicy {
 public static bool TryGetCap(SessionIdentity session, Dictionary<uint,float> targets, float? systemCap, out float cap) {
  if(!session.IsSystemSounds) return targets.TryGetValue(session.PID,out cap);
  cap=systemCap.GetValueOrDefault();
  return systemCap.HasValue;
 }
 public static string SkipReason(SessionIdentity candidate, IEnumerable<SessionIdentity> endpoint, bool voice) {
  if(!candidate.IsSystemSounds && candidate.OwnerStatus!=0) return "shared or unknown process ownership";
  foreach(SessionIdentity other in endpoint) {
   if(Object.ReferenceEquals(candidate,other)) continue;
   if(candidate.Group!=Guid.Empty && candidate.Group==other.Group) return "grouped with another audio session";
   if(candidate.IsSystemSounds && !String.IsNullOrEmpty(candidate.Identifier) && candidate.Identifier==other.Identifier) return "shared audio session identifier";
   if(!voice && candidate.PID!=0 && candidate.PID==other.PID) return "multiple sessions from the same process";
  }
  return null;
 }
 public static float Ceiling(float current, float cap) { return Math.Min(current,cap); }
}
public sealed class VoiceDefaults {
 readonly HashSet<string> initialized = new HashSet<string>();
 public float Desired(string instance, float current, float configured) {
  return initialized.Contains(instance) ? current : configured;
 }
 public void Commit(string instance) { initialized.Add(instance); }
 public void Reset() { initialized.Clear(); }
}
public static class Audio {
 static readonly VoiceDefaults voiceDefaults = new VoiceDefaults();
 public static void ResetVoice() { voiceDefaults.Reset(); }
 internal static void Release(object o) { if(o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
 internal static bool IsSystemSounds(IControl control) {
  int result=control.IsSystemSoundsSession();
  Marshal.ThrowExceptionForHR(result);
  return result==0;
 }
 public static Row[] Scan(Dictionary<uint,float> targets, float? systemCap, Dictionary<uint,float> voices) {
  var rows = new List<Row>(); object root = null; ICollection devices = null;
  if(targets.Count==0 && !systemCap.HasValue && voices.Count==0) return rows.ToArray();
  try {
   root = new Enumerator(); ((IDevices)root).EnumAudioEndpoints(0, 1, out devices);
   uint count; devices.GetCount(out count);
   for(uint d=0; d<count; d++) {
    IDevice device = null; object manager = null; ISessions sessions = null;
    var identities = new List<SessionIdentity>();
    var controls = new List<IControl>();
    try {
     devices.Item(d, out device); Guid iid = typeof(IManager).GUID;
     device.Activate(ref iid, 23, IntPtr.Zero, out manager);
     ((IManager)manager).GetSessionEnumerator(out sessions); int n; sessions.GetCount(out n);
     // Inspect every live session on this output before changing any volume.
     for(int s=0; s<n; s++) {
      IControl control = null;
      try {
       sessions.GetSession(s, out control);
       int state; control.GetState(out state);
       if(state==2) continue;
       var identity=new SessionIdentity();
       identity.IsSystemSounds=IsSystemSounds(control);
       identity.OwnerStatus=control.GetProcessId(out identity.PID);
       if(!identity.IsSystemSounds) Marshal.ThrowExceptionForHR(identity.OwnerStatus);
       control.GetGroupingParam(out identity.Group);
       control.GetSessionIdentifier(out identity.Identifier);
       identities.Add(identity); controls.Add(control); control=null;
      } finally { Release(control); }
     }
     for(int s=0; s<identities.Count; s++) {
      SessionIdentity identity=identities[s]; float cap;
      bool voice=false;
      if(!NotificationPolicy.TryGetCap(identity,targets,systemCap,out cap)) {
       if(identity.IsSystemSounds || !voices.TryGetValue(identity.PID,out cap)) continue;
       voice=true;
      }
      var row=new Row { IsSystemSounds=identity.IsSystemSounds, IsVoice=voice, PID=identity.PID, Device=(int)d };
      rows.Add(row);
      try {
       var volume=(IVolume)controls[s]; float old; volume.GetMasterVolume(out old);
       row.Percent=old*100;
       row.SkipReason=NotificationPolicy.SkipReason(identity,identities,voice);
       if(row.SkipReason!=null) continue;
       string instance=null;
       if(voice) {
        controls[s].GetSessionInstanceIdentifier(out instance);
        if(String.IsNullOrEmpty(instance)) { row.SkipReason="voice instance identifier unavailable"; continue; }
       }
       float target=voice ? voiceDefaults.Desired(instance,old,cap) : NotificationPolicy.Ceiling(old,cap);
       if(Math.Abs(old-target)>0.00001f) {
        Guid context=Guid.Empty;
        volume.SetMasterVolume(target,ref context);
        row.Changed=true;
        row.Percent=target*100;
       }
       if(voice) voiceDefaults.Commit(instance);
      } catch(COMException ex) { row.Error=ex.Message; }
     }
    } catch(COMException) {
     rows.Add(new Row { Device=(int)d, SkipReason="output session inventory unavailable" });
    } finally {
     foreach(IControl control in controls) Release(control);
     Release(sessions); Release(manager); Release(device);
    }
   }
  } finally { Release(devices); Release(root); }
  return rows.ToArray();
 }
}
}
