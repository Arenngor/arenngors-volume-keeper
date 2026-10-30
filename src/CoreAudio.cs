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
 public int OnGroupingParamChanged(ref Guid grouping, ref Guid context) { return 0; }
 public int OnStateChanged(int state) { Notify(); return 0; }
 public int OnSessionDisconnected(int reason) { Notify(); return 0; }
 public void Observe(uint[] pids) {
  var wanted = new HashSet<uint>(pids);
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
      sessions.GetSession(i,out control); uint pid; control.GetProcessId(out pid);
      if(!wanted.Contains(pid)) continue;
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
 void GetProcessId(out uint pid);
}
[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume {
 void SetMasterVolume(float volume, ref Guid context); void GetMasterVolume(out float volume);
 void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
public class Row { public uint PID; public int Device; public float Percent; public bool Changed; public string Error; }
public sealed class VolumePolicy {
 readonly HashSet<string> initializedVoice = new HashSet<string>();
 public float Desired(string instance, float current, float configured, bool voice) {
  if(voice) return initializedVoice.Contains(instance) ? current : configured;
  return Math.Min(current,configured);
 }
 public void Committed(string instance,bool voice) { if(voice) initializedVoice.Add(instance); }
 public void Reset() { initializedVoice.Clear(); }
}
public static class Audio {
 static readonly VolumePolicy policy = new VolumePolicy();
 public static void ResetVoice() { policy.Reset(); }
 internal static void Release(object o) { if(o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
 public static Row[] Scan(Dictionary<uint,float> targets, bool apply, bool voice) {
  var rows = new List<Row>(); object root = null; ICollection devices = null;
  try {
   root = new Enumerator(); ((IDevices)root).EnumAudioEndpoints(0, 1, out devices);
   uint count; devices.GetCount(out count);
   for(uint d=0; d<count; d++) {
    IDevice device = null; object manager = null; ISessions sessions = null;
    try {
     devices.Item(d, out device); Guid iid = typeof(IManager).GUID;
     device.Activate(ref iid, 23, IntPtr.Zero, out manager);
     ((IManager)manager).GetSessionEnumerator(out sessions); int n; sessions.GetCount(out n);
     for(int s=0; s<n; s++) {
      IControl control = null;
      try {
       sessions.GetSession(s, out control); uint pid; control.GetProcessId(out pid);
       if(!targets.ContainsKey(pid)) continue;
       string instance; control.GetSessionInstanceIdentifier(out instance);
       var volume = (IVolume)control; float old; volume.GetMasterVolume(out old);
       float target = policy.Desired(instance,old,targets[pid],voice);
       bool changed = apply && Math.Abs(old-target)>0.00001f;
       if(changed) {
        try { Guid context = Guid.Empty; volume.SetMasterVolume(target, ref context); }
        catch(COMException ex) {
         rows.Add(new Row { PID=pid, Device=(int)d, Percent=old*100, Changed=false, Error=ex.Message });
         continue;
        }
       }
       if(apply) policy.Committed(instance,voice);
       rows.Add(new Row { PID=pid, Device=(int)d, Percent=old*100, Changed=changed });
      } catch(COMException) {} finally { Release(control); }
     }
    } catch(COMException) {} finally { Release(sessions); Release(manager); Release(device); }
   }
  } finally { Release(devices); Release(root); }
  return rows.ToArray();
 }
}
}
