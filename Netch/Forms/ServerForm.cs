using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Netch.Models;
using Netch.Utils;

namespace Netch.Forms
{
    [DesignerCategory("Code")]
    public abstract class ServerForm : Form
    {
        private readonly TableLayoutPanel _fields = new()
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top,
            ColumnCount = 2, Padding = new Padding(12)
        };
        private readonly List<(Control Input, Func<string, bool> Check, Action Save)> _bindings = new();
        private readonly TextBox _remark;
        private readonly TextBox _address;
        private readonly TextBox _port;

        protected ServerForm()
        {
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(565, 500);
            MinimumSize = new Size(415, 300);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(_fields);

            var save = new Button { Name = "SaveButton", Text = "Save", AutoSize = true };
            var cancel = new Button { Name = "CancelButton", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 4, 12, 8)
            };
            actions.Controls.AddRange(new Control[] { cancel, save });
            Controls.Add(scroll);
            Controls.Add(actions);
            AcceptButton = save;
            CancelButton = cancel;
            save.Click += (_, _) => SaveServer();

            _remark = AddTextBox("Remark", "Remark", _ => true, s => Server.Remark = s, "");
            _address = AddTextBox("Address", "Address", s => !string.IsNullOrWhiteSpace(s), s => Server.Hostname = s.Trim(), "");
            _port = AddTextBox("Port", "Port", s => ushort.TryParse(s, out var port) && port > 0, s => Server.Port = ushort.Parse(s), "");
        }

        protected abstract string TypeName { get; }
        protected Server Server { get; set; } = null!;

        public new DialogResult ShowDialog()
        {
            Text = TypeName;
            _remark.Text = Server.Remark;
            _address.Text = Server.Hostname;
            _port.Text = Server.Port == 0 ? "" : Server.Port.ToString();
            i18N.TranslateForm(this);
            return base.ShowDialog(Global.MainForm);
        }

        protected void CreateTextBox(string name, string caption, Func<string, bool> check, Action<string> save, string? value)
        {
            AddTextBox(name, caption, check, save, value);
        }

        private TextBox AddTextBox(string name, string caption, Func<string, bool> check, Action<string> save, string? value)
        {
            var input = new TextBox { Name = name + "TextBox", Text = value ?? "", UseSystemPasswordChar = name == "Password" };
            AddRow(name, caption, input);
            _bindings.Add((input, check, () => save(input.Text)));
            return input;
        }

        protected void CreateComboBox(string name, string caption, List<string> values, Action<string> save, string? value)
        {
            var input = new ComboBox { Name = name + "ComboBox", DropDownStyle = ComboBoxStyle.DropDownList };
            input.Items.AddRange(values.ToArray());
            if (value != null && !input.Items.Contains(value))
                input.Items.Add(value);
            input.SelectedItem = value ?? (input.Items.Count > 0 ? input.Items[0] : null);
            AddRow(name, caption, input);
            _bindings.Add((input, _ => true, () => save(input.Text)));
        }

        private void AddRow(string name, string caption, Control input)
        {
            var row = _fields.RowCount++;
            _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label
            {
                AutoSize = true, Anchor = AnchorStyles.Left, Name = name + "Label", Text = caption,
                Margin = new Padding(0, 6, 14, 6)
            };
            input.AccessibleName = caption;
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 4, 0, 4);
            _fields.Controls.Add(label, 0, row);
            _fields.Controls.Add(input, 1, row);
        }

        private void SaveServer()
        {
            foreach (var (input, check, _) in _bindings)
            {
                if (check(input.Text))
                    continue;

                MessageBox.Show(this, i18N.TranslateFormat("Invalid value for {0}.", input.AccessibleName ?? ""), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                input.Focus();
                return;
            }

            foreach (var (_, _, save) in _bindings)
                save();

            if (!Global.Settings.Server.Contains(Server))
                Global.Settings.Server.Add(Server);

            DialogResult = DialogResult.OK;
        }
    }
}
