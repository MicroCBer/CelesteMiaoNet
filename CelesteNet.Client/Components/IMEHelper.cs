using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CelesteNet.Client.Components {
    using System;
    using System.Drawing;
    using System.Windows.Forms;
    using static System.Net.Mime.MediaTypeNames;

    public delegate void Callback(string text);

    public static class IMEHelper {
        static bool createdInput = false;

        public static void fixIMEForCeleste() {
            if (!createdInput) {
                createdInput = true;
                CreateInvisibleFormWithInputBox((text) => { });
            }
        }

        static string CreateInvisibleFormWithInputBox(Callback callback) {
            string result = String.Empty;

            IntPtr celesteHandle = NativeMethods.FindWindow(null, "Celeste");
            if (celesteHandle == IntPtr.Zero) {
                return "";
            }

            Form invisibleForm = new Form() {
                Width = 300,
                Height = 30,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = 1
            };

            TextBox textBox = new TextBox() {
                Location = new Point(0, 0),
                Size = new Size(300, 30),
                TabStop = false
            };
            invisibleForm.Controls.Add(textBox);

            textBox.TextChanged += (sender, e) => {
                callback(textBox.Text);
            };

            textBox.PreviewKeyDown += (sender, e) => {
                if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Escape) {
                    result = textBox.Text;
                    invisibleForm.Close();
                }
            };

            invisibleForm.Deactivate += (sender, e) => {
                result = textBox.Text;
                invisibleForm.Close();
            };

            invisibleForm.Shown += (sender, e) => {
                invisibleForm.Opacity = 1;
                textBox.Size = new Size(0, 0);
                textBox.Focus();
            };

            NativeMethods.SetParent(invisibleForm.Handle, celesteHandle);
            NativeMethods.SetWindowPos(invisibleForm.Handle, IntPtr.Zero, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);

            System.Windows.Forms.Application.Run(invisibleForm);

            return result;
        }


        internal static class NativeMethods {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
        }

        [Flags]
        internal enum SetWindowPosFlags : uint {
            SWP_NOSIZE = 0x0001,
            SWP_NOZORDER = 0x0004,
            SWP_NOACTIVATE = 0x0010
        }
    }

}
