using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kermalis.VGMusicStudio.UI
{
    [DesignerCategory("")]
    internal class AssemblerDialog : ThemedForm
    {
        private Assembler _assembler;
        private readonly ThemedButton _previewButton;
        private readonly ValueTextBox _offsetValueBox;
        private readonly ThemedLabel _sizeLabel;
        private readonly ThemedTextBox _headerLabelTextBox;
        private readonly DataGridView _addedDefsGrid;

        public AssemblerDialog()
        {
            var openButton = new ThemedButton
            {
                Location = new Point(150, 0),
                Text = "TODO"//Strings.AssemblerOpenFile
            };
            openButton.Click += OpenASM;
            _previewButton = new ThemedButton
            {
                Enabled = false,
                Location = new Point(150, 50),
                Size = new Size(120, 23),
                Text = "TODO"//Strings.AssemblerPreviewSong
            };
            _previewButton.Click += PreviewSong;
            _sizeLabel = new ThemedLabel
            {
                Location = new Point(0, 100),
                Size = new Size(150, 23)
            };
            _offsetValueBox = new ValueTextBox
            {
                Hexadecimal = true,
                Maximum = Core.GBA.Utils.CartridgeCapacity - 1
            };
            _headerLabelTextBox = new ThemedTextBox
            {
                Location = new Point(0, 50),
                Size = new Size(150, 22)
            };
            _addedDefsGrid = new DataGridView
            {
                ColumnCount = 2,
                Location = new Point(0, 150),
                MultiSelect = false
            };
            _addedDefsGrid.Columns[0].Name = "TODO";//Strings.AssemblerDefinition;
            _addedDefsGrid.Columns[1].Name = "TODO";//Strings.AssemblerValue;
            _addedDefsGrid.Columns[1].DefaultCellStyle.NullValue = "0";
            int voiceTableOffset = ((Core.GBA.MP2K.Player)Engine.Instance.Player).VoiceTableOffset;
            if (voiceTableOffset == -1)
            {
                voiceTableOffset = 0;
            }
            voiceTableOffset += Core.GBA.Utils.CartridgeOffset;
            _addedDefsGrid.Rows.Add(new string[] { "voicegroup000", $"0x{voiceTableOffset:X7}" });
            _addedDefsGrid.CellValueChanged += AddedDefsGrid_CellValueChanged;

            Controls.AddRange(new Control[] { openButton, _previewButton, _sizeLabel, _offsetValueBox, _headerLabelTextBox, _addedDefsGrid });
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Size = new Size(600, 400);
            //Text = $"{Util.Utils.ProgramName} ― {Strings.AssemblerTitle}";
            Text = $"{Util.Utils.ProgramName} ― {"TODO"}";
        }

        private void AddedDefsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewCell cell = _addedDefsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Value == null)
            {
                return;
            }

            if (e.ColumnIndex == 0)
            {
                if (char.IsDigit(cell.Value.ToString()[0]))
                {
                    //FlexibleMessageBox.Show(Strings.AssemblerErrorDefinitionDigit, Strings.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cell.Value = cell.Value.ToString().Substring(1);
                }
            }
            else
            {
                if (!Util.Utils.TryParseValue(cell.Value.ToString(), int.MinValue, int.MaxValue, out _))
                {
                    //FlexibleMessageBox.Show(string.Format(Strings.AssemblerErrorInvalidValue, cell.Value), Strings.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cell.Value = null;
                }
            }
        }

        private void PreviewSong(object sender, EventArgs e)
        {
            ((MainForm)Owner).PreviewMP2KAssemblerSong(_assembler, Path.GetFileName(_assembler.FileName), _assembler[Assembler.FixLabel(Path.GetFileNameWithoutExtension(_assembler.FileName))]);
        }

        private void OpenASM(object sender, EventArgs e)
        {
            var d = new OpenFileDialog
            {
                Title = "TODO",//Strings.TitleOpenASM,
                Filter = "TODO|*.s"//$"{Strings.FilterOpenASM}|*.s"
            };
            if (d.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                var s = new Dictionary<string, int>();
                foreach (DataGridViewRow r in _addedDefsGrid.Rows.Cast<DataGridViewRow>())
                {
                    if (r.Cells[0].Value == null || r.Cells[1].Value == null)
                    {
                        continue;
                    }
                    s.Add(r.Cells[0].Value.ToString(), (int)Util.Utils.ParseValue("TODO", r.Cells[1].Value.ToString(), int.MinValue, int.MaxValue));
                }
                _assembler = new Assembler(d.FileName, (int)(Core.GBA.Utils.CartridgeOffset + _offsetValueBox.Value), Endianness.LittleEndian, s);
                _headerLabelTextBox.Text = Assembler.FixLabel(Path.GetFileNameWithoutExtension(d.FileName));
                //sizeLabel.Text = string.Format(Strings.AssemblerSizeInBytes, assembler.BinaryLength);
                _sizeLabel.Text = string.Format("TODO Size in bytes: 0x{0:X}", _assembler.BinaryLength);
                _previewButton.Enabled = true;
            }
            catch (Exception ex)
            {
                //FlexibleMessageBox.Show(ex.Message, Strings.TitleAssemblerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                FlexibleMessageBox.Show(ex, "TODO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}