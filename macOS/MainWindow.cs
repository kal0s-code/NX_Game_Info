using System;
using ObjCRuntime;
using Foundation;
using AppKit;

#pragma warning disable CA1422 // TODO: revisit NSWindow usage for macOS 15 compatibility.

namespace NX_Game_Info
{
    public partial class MainWindow : NSWindow
    {
        public MainWindow(NativeHandle handle) : base(handle)
        {
        }

        [Export("initWithCoder:")]
        public MainWindow(NSCoder coder) : base(coder)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
        }
    }
}
#pragma warning restore CA1422
