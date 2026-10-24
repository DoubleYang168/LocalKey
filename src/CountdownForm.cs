using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalKey
{
    internal sealed class CountdownForm : Form
    {
        private readonly Label message;
        internal CountdownForm()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            BackColor = Theme.Ink; ForeColor = Color.White; Size = new Size(440, 110);
            StartPosition = FormStartPosition.Manual;
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Right - Width - 20, screen.Bottom - Height - 20);
            message = new Label { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 10),
                Font = new Font("Microsoft YaHei UI", 11), ForeColor = Color.White };
            Controls.Add(message); Enabled = false;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }
        internal void UpdateSeconds(int seconds, bool clicked)
        { message.Text = "请点击目标密码框 · " + seconds + " 秒后填写\n" + (clicked ? "已选中输入框，请保持焦点。Esc 取消。" : "需要重新点击一次。Esc 取消。不会自动提交。"); }
    }
}
