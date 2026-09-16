using System.Runtime.InteropServices;
namespace FullLengthPlayer;
internal static class TouchVerification
{
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    internal static async Task Run(MainForm form, string media)
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Fullscreen touch: " + message); }
        async Task Until(Func<bool> good, string message)
        {
            long deadline=Environment.TickCount64+10000;
            while (!good() || form.Master.SeekingTogether) { if(Environment.TickCount64>deadline) throw new InvalidOperationException(message); await Task.Delay(50); }
        }
        async Task Click(Point point)
        {
            Cursor.Position=point;
            mouse_event(2,0,0,0,UIntPtr.Zero); await Task.Delay(30);
            mouse_event(4,0,0,0,UIntPtr.Zero); await Task.Delay(60);
        }
        form.Reaction.LoadVideo(media); form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!,"source video.mkv"));
        var a=form.Reaction.Player!; var b=form.Source.Player!;
        await Until(()=>a.Number("duration")>30 && b.Number("duration")>30 && a.Get("pause")=="yes" && b.Get("pause")=="yes", "Paused load failed.");
        form.Master.SetOffset(2); form.Master.SeekReaction(12);
        await Until(()=>Math.Abs(a.Number("time-pos")-12)<.1, "Setup seek failed.");
        form.ToggleFullscreen(); form.Activate(); await Task.Delay(200);
        Point At(double x,double y)=>form.PointToScreen(new Point((int)(form.ClientSize.Width*x),(int)(form.ClientSize.Height*y)));
        // Actual Windows mouse input above embedded libmpv A and B surfaces.
        await Click(At(.5,.1));
        await Until(()=>a.Get("pause")=="no" && b.Get("pause")=="no", "Centre tap over reaction failed.");
        await Task.Delay(SystemInformation.DoubleClickTime+80);
        await Click(At(.5,.7));
        await Until(()=>a.Get("pause")=="yes" && b.Get("pause")=="yes", "Centre tap over source failed.");
        double before=a.Number("time-pos");
        await Click(At(.1,.4)); Check(Math.Abs(a.Number("time-pos")-before)<.1, "Single side tap sought.");
        await Click(At(.1,.4));
        await Until(()=>Math.Abs(a.Number("time-pos")-(before-5))<.1, "Left double tap did not seek both.");
        Check(Math.Abs(b.Number("time-pos")-a.Number("time-pos")-2)<.1 && form.Master.Locked, "Tap seek lost locked offset.");
        await Click(At(.9,.4)); await Click(At(.9,.4));
        await Until(()=>Math.Abs(a.Number("time-pos")-before)<.1, "Right double tap failed.");
        Check(a.Get("pause")=="yes" && b.Get("pause")=="yes", "Seek toggled pause.");
        foreach(var key in new[]{Keys.D,Keys.S,Keys.A,Keys.G})
        {
            form.HandleShortcut(key,Point.Empty);
            Check(form.SpeedToast.Visible && form.SpeedToast.Caption==a.Number("speed").ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"×", "Wrong/missing speed notice.");
            Check(a.Number("speed")==b.Number("speed"), "Speed changed one player only.");
        }
        form.SpeedToast.Advance(Environment.TickCount64+1000,true); Check(form.SpeedToast.Visible && form.SpeedToast.Opacity<.92, "Notice did not fade.");
        form.SpeedToast.Advance(Environment.TickCount64+1300,true); Check(!form.SpeedToast.Visible, "Notice did not hide.");
        var reactionPoint = At(.5,.1); var sourcePoint = At(.5,.7);
        form.Reaction.SetVolume(50); form.Source.SetVolume(75);
        form.HandleVolumeWheel(reactionPoint,120);
        Check(form.SpeedToast.Caption == "Reaction A · 55%" && a.Number("volume")==55 && b.Number("volume")==75, "Reaction volume feedback/target wrong.");
        form.HandleVolumeWheel(sourcePoint,-120);
        Check(form.SpeedToast.Caption == "Source B · 70%" && a.Number("volume")==55 && b.Number("volume")==70, "Source volume feedback/target wrong.");
        form.HandleVolumeWheel(sourcePoint,12000);
        Check(form.SpeedToast.Caption == "Source B · 100%", "Volume feedback not clamped.");
        double priorSpeed = a.Number("speed");
        form.HandleShortcut(Keys.H,Point.Empty);
        Check(form.Replay.Active && form.SpeedToast.Visible && form.SpeedToast.Caption=="What Did They Say? · 1×", "Replay trigger feedback missing.");
        await Until(()=>a.Get("pause")=="no", "Replay did not resume.");
        form.HandleShortcut(Keys.H,Point.Empty);
        Check(!form.Replay.Active && form.SpeedToast.Caption.StartsWith("Replay ended · ") && a.Number("speed")==priorSpeed, "Replay cancellation feedback/restoration wrong.");
        Check(a.Number("volume")==55 && b.Number("volume")==100, "Replay feedback changed restored volumes.");
        Check(Form.ActiveForm == form, "Feedback stole keyboard focus.");
        // Pure gesture edge cases: centre double tap toggles once; unrelated taps don't pair.
        var gestures=new FullscreenGestures(); var size=new Size(900,600);
        Check(gestures.Tap(new Point(450,300),size,0,500,24)==1 && gestures.Tap(new Point(450,300),size,100,500,24)==0,"Centre double tap toggles twice.");
        Check(gestures.Tap(new Point(10,300),size,600,500,24)==0 && gestures.Tap(new Point(890,300),size,700,500,24)==0,"Cross-side taps paired.");
        Check(gestures.Tap(new Point(890,300),size,1300,500,24)==0,"Slow taps paired.");
        form.RevealFullscreen();
        var timeline=form.Hud.Timeline.PointToScreen(new Point(20,10));
        Check(!form.FullscreenPointer(0x0201,timeline,Environment.TickCount64),"Gesture stole HUD input.");
        form.Hud.ExitButton.PerformClick(); Check(!form.Fullscreen && !form.SpeedToast.Visible,"Touch exit failed.");
        Check(!form.FullscreenPointer(0x0201,At(.5,.5),Environment.TickCount64),"Windowed click intercepted.");
    }
}
