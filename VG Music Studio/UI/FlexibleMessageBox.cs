using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Kermalis.VGMusicStudio.UI
{
    /*  FlexibleMessageBox – A flexible replacement for the .NET MessageBox
     * 
     *  Author:         Jörg Reichert (public@jreichert.de)
     *  Contributors:   Thanks to: David Hall, Roink
     *  Version:        1.3
     *  Published at:   http://www.codeproject.com/Articles/601900/FlexibleMessageBox
     *  
     ************************************************************************************************************
     * Features:
     *  - It can be simply used instead of MessageBox since all important static "Show"-Functions are supported
     *  - It is small, only one source file, which could be added easily to each solution 
     *  - It can be resized and the content is correctly word-wrapped
     *  - It tries to auto-size the width to show the longest text row
     *  - It never exceeds the current desktop working area
     *  - It displays a vertical scrollbar when needed
     *  - It does support hyperlinks in text
     * 
     *  Because the interface is identical to MessageBox, you can add this single source file to your project 
     *  and use the FlexibleMessageBox almost everywhere you use a standard MessageBox. 
     *  The goal was NOT to produce as many features as possible but to provide a simple replacement to fit my 
     *  own needs. Feel free to add additional features on your own, but please left my credits in this class.
     * 
     ************************************************************************************************************
     * Usage examples:
     * 
     *  FlexibleMessageBox.Show("Just a text");
     * 
     *  FlexibleMessageBox.Show("A text", 
     *                          "A caption"); 
     *  
     *  FlexibleMessageBox.Show("Some text with a link: www.google.com", 
     *                          "Some caption",
     *                          MessageBoxButtons.AbortRetryIgnore, 
     *                          MessageBoxIcon.Information,
     *                          MessageBoxDefaultButton.Button2);
     *  
     *  var dialogResult = FlexibleMessageBox.Show("Do you know the answer to life the universe and everything?", 
     *                                             "One short question",
     *                                             MessageBoxButtons.YesNo);     
     * 
     ************************************************************************************************************
     *  THE SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS", WITHOUT WARRANTY
     *  OF ANY KIND, EXPRESS OR IMPLIED. IN NO EVENT SHALL THE AUTHOR BE
     *  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY ARISING FROM,
     *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OF THIS
     *  SOFTWARE.
     *  
     ************************************************************************************************************
     * History:
     *  Version 1.3 - 19.Dezember 2014
     *  - Added refactoring function GetButtonText()
     *  - Used CurrentUICulture instead of InstalledUICulture
     *  - Added more button localizations. Supported languages are now: ENGLISH, GERMAN, SPANISH, ITALIAN
     *  - Added standard MessageBox handling for "copy to clipboard" with <Ctrl> + <C> and <Ctrl> + <Insert>
     *  - Tab handling is now corrected (only tabbing over the visible buttons)
     *  - Added standard MessageBox handling for ALT-Keyboard shortcuts
     *  - SetDialogSizes: Refactored completely: Corrected sizing and added caption driven sizing
     * 
     *  Version 1.2 - 10.August 2013
     *   - Do not ShowInTaskbar anymore (original MessageBox is also hidden in taskbar)
     *   - Added handling for Escape-Button
     *   - Adapted top right close button (red X) to behave like MessageBox (but hidden instead of deactivated)
     * 
     *  Version 1.1 - 14.June 2013
     *   - Some Refactoring
     *   - Added internal form class
     *   - Added missing code comments, etc.
     *  
     *  Version 1.0 - 15.April 2013
     *   - Initial Version
    */

    internal class FlexibleMessageBox
    {
        #region Public statics

        /// <summary>
        /// Defines the maximum width for all FlexibleMessageBox instances in percent of the working area.
        /// 
        /// Allowed values are 0.2 - 1.0 where: 
        /// 0.2 means:  The FlexibleMessageBox can be at most half as wide as the working area.
        /// 1.0 means:  The FlexibleMessageBox can be as wide as the working area.
        /// 
        /// Default is: 70% of the working area width.
        /// </summary>
        public static double MAX_WIDTH_FACTOR = 0.7;

        /// <summary>
        /// Defines the maximum height for all FlexibleMessageBox instances in percent of the working area.
        /// 
        /// Allowed values are 0.2 - 1.0 where: 
        /// 0.2 means:  The FlexibleMessageBox can be at most half as high as the working area.
        /// 1.0 means:  The FlexibleMessageBox can be as high as the working area.
        /// 
        /// Default is: 90% of the working area height.
        /// </summary>
        public static double MAX_HEIGHT_FACTOR = 0.9;

        /// <summary>
        /// Defines the font for all FlexibleMessageBox instances.
        /// 
        /// Default is: Theme.Font
        /// </summary>
        public static Font FONT = Theme.Font;

        #endregion

        #region Public show functions

        private static string ParseException(Exception ex)
        {
            return string.Format("Error Details:{1}{1}{0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
        }

        public static DialogResult Show(string text)
        {
            return FlexibleMessageBoxForm.Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(IWin32Window owner, string text)
        {
            return FlexibleMessageBoxForm.Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(string text, string caption)
        {
            return FlexibleMessageBoxForm.Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(Exception ex, string caption)
        {
            return FlexibleMessageBoxForm.Show(null, ParseException(ex), caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(IWin32Window owner, string text, string caption)
        {
            return FlexibleMessageBoxForm.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return FlexibleMessageBoxForm.Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
        {
            return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return FlexibleMessageBoxForm.Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(Exception ex, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return FlexibleMessageBoxForm.Show(null, ParseException(ex), caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return FlexibleMessageBoxForm.Show(null, text, caption, buttons, icon, defaultButton);
        }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, icon, defaultButton);
        }

        #endregion

        #region Internal form class

        class FlexibleMessageBoxForm : ThemedForm
        {
            IContainer components = null;

            protected override void Dispose(bool disposing)
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
                base.Dispose(disposing);
            }
            void InitializeComponent()
            {
                components = new Container();
                button1 = new ThemedButton();
                richTextBoxMessage = new ThemedRichTextBox();
                FlexibleMessageBoxFormBindingSource = new BindingSource(components);
                panel1 = new ThemedPanel();
                pictureBoxForIcon = new PictureBox();
                button2 = new ThemedButton();
                button3 = new ThemedButton();
                ((ISupportInitialize)(FlexibleMessageBoxFormBindingSource)).BeginInit();
                panel1.SuspendLayout();
                ((ISupportInitialize)(pictureBoxForIcon)).BeginInit();
                SuspendLayout();
                // 
                // button1
                // 
                button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                button1.AutoSize = true;
                button1.DialogResult = DialogResult.OK;
                button1.Location = new Point(11, 67);
                button1.MinimumSize = new Size(0, 24);
                button1.Name = "button1";
                button1.Size = new Size(75, 24);
                button1.TabIndex = 2;
                button1.Text = "OK";
                button1.UseVisualStyleBackColor = true;
                button1.Visible = false;
                // 
                // richTextBoxMessage
                // 
                richTextBoxMessage.Anchor = (((AnchorStyles.Top | AnchorStyles.Bottom)
                | AnchorStyles.Left)
                | AnchorStyles.Right);
                richTextBoxMessage.BorderStyle = BorderStyle.None;
                richTextBoxMessage.DataBindings.Add(new Binding("Text", FlexibleMessageBoxFormBindingSource, "MessageText", true, DataSourceUpdateMode.OnPropertyChanged));
                richTextBoxMessage.Font = new Font(Theme.Font.FontFamily, 9);
                richTextBoxMessage.Location = new Point(50, 26);
                richTextBoxMessage.Margin = new Padding(0);
                richTextBoxMessage.Name = "richTextBoxMessage";
                richTextBoxMessage.ReadOnly = true;
                richTextBoxMessage.ScrollBars = RichTextBoxScrollBars.Vertical;
                richTextBoxMessage.Size = new Size(200, 20);
                richTextBoxMessage.TabIndex = 0;
                richTextBoxMessage.TabStop = false;
                richTextBoxMessage.Text = "<Message>";
                richTextBoxMessage.LinkClicked += new LinkClickedEventHandler(LinkClicked);
                // 
                // panel1
                // 
                panel1.Anchor = (((AnchorStyles.Top | AnchorStyles.Bottom)
                | AnchorStyles.Left)
                | AnchorStyles.Right);
                panel1.Controls.Add(pictureBoxForIcon);
                panel1.Controls.Add(richTextBoxMessage);
                panel1.Location = new Point(-3, -4);
                panel1.Name = "panel1";
                panel1.Size = new Size(268, 59);
                panel1.TabIndex = 1;
                // 
                // pictureBoxForIcon
                // 
                pictureBoxForIcon.BackColor = Color.Transparent;
                pictureBoxForIcon.Location = new Point(15, 19);
                pictureBoxForIcon.Name = "pictureBoxForIcon";
                pictureBoxForIcon.Size = new Size(32, 32);
                pictureBoxForIcon.TabIndex = 8;
                pictureBoxForIcon.TabStop = false;
                // 
                // button2
                // 
                button2.Anchor = (AnchorStyles.Bottom | AnchorStyles.Right);
                button2.DialogResult = DialogResult.OK;
                button2.Location = new Point(92, 67);
                button2.MinimumSize = new Size(0, 24);
                button2.Name = "button2";
                button2.Size = new Size(75, 24);
                button2.TabIndex = 3;
                button2.Text = "OK";
                button2.UseVisualStyleBackColor = true;
                button2.Visible = false;
                // 
                // button3
                // 
                button3.Anchor = (AnchorStyles.Bottom | AnchorStyles.Right);
                button3.AutoSize = true;
                button3.DialogResult = DialogResult.OK;
                button3.Location = new Point(173, 67);
                button3.MinimumSize = new Size(0, 24);
                button3.Name = "button3";
                button3.Size = new Size(75, 24);
                button3.TabIndex = 0;
                button3.Text = "OK";
                button3.UseVisualStyleBackColor = true;
                button3.Visible = false;
                // 
                // FlexibleMessageBoxForm
                // 
                AutoScaleDimensions = new SizeF(6F, 13F);
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(260, 102);
                Controls.Add(button3);
                Controls.Add(button2);
                Controls.Add(panel1);
                Controls.Add(button1);
                DataBindings.Add(new Binding("Text", FlexibleMessageBoxFormBindingSource, "CaptionText", true));
                Icon = Properties.Resources.Icon;
                MaximizeBox = false;
                MinimizeBox = false;
                MinimumSize = new Size(276, 140);
                Name = "FlexibleMessageBoxForm";
                SizeGripStyle = SizeGripStyle.Show;
                StartPosition = FormStartPosition.CenterParent;
                Text = "<Caption>";
                Shown += new EventHandler(FlexibleMessageBoxForm_Shown);
                ((ISupportInitialize)(FlexibleMessageBoxFormBindingSource)).EndInit();
                panel1.ResumeLayout(false);
                ((ISupportInitialize)(pictureBoxForIcon)).EndInit();
                ResumeLayout(false);
                PerformLayout();
            }

            ThemedButton button1, button2, button3;
            private BindingSource FlexibleMessageBoxFormBindingSource;
            ThemedRichTextBox richTextBoxMessage;
            ThemedPanel panel1;
            private PictureBox pictureBoxForIcon;

            #region Private constants

            //These separators are used for the "copy to clipboard" standard operation, triggered by Ctrl + C (behavior and clipboard format is like in a standard MessageBox)
            static readonly String STANDARD_MESSAGEBOX_SEPARATOR_LINES = "---------------------------\n";
            static readonly String STANDARD_MESSAGEBOX_SEPARATOR_SPACES = "   ";

            //These are the possible buttons (in a standard MessageBox)
            private enum ButtonID { OK = 0, CANCEL, YES, NO, ABORT, RETRY, IGNORE };

            //These are the buttons texts for different languages. 
            //If you want to add a new language, add it here and in the GetButtonText-Function
            private enum TwoLetterISOLanguageID { en, de, es, it };
            static readonly String[] BUTTON_TEXTS_ENGLISH_EN = { "OK", "Cancel", "&Yes", "&No", "&Abort", "&Retry", "&Ignore" }; //Note: This is also the fallback language
            static readonly String[] BUTTON_TEXTS_GERMAN_DE = { "OK", "Abbrechen", "&Ja", "&Nein", "&Abbrechen", "&Wiederholen", "&Ignorieren" };
            static readonly String[] BUTTON_TEXTS_SPANISH_ES = { "Aceptar", "Cancelar", "&Sí", "&No", "&Abortar", "&Reintentar", "&Ignorar" };
            static readonly String[] BUTTON_TEXTS_ITALIAN_IT = { "OK", "Annulla", "&Sì", "&No", "&Interrompi", "&Riprova", "&Ignora" };

            #endregion

            #region Private members

            MessageBoxDefaultButton defaultButton;
            int visibleButtonsCount;
            readonly TwoLetterISOLanguageID languageID = TwoLetterISOLanguageID.en;

            #endregion

            #region Private constructor

            private FlexibleMessageBoxForm()
            {
                InitializeComponent();

                //Try to evaluate the language. If this fails, the fallback language English will be used
                Enum.TryParse(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, out languageID);

                KeyPreview = true;
                KeyUp += FlexibleMessageBoxForm_KeyUp;
            }

            #endregion

            #region Private helper functions

            static string[] GetStringRows(string message)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return null;
                }

                var messageRows = message.Split(new char[] { '\n' }, StringSplitOptions.None);
                return messageRows;
            }

            string GetButtonText(ButtonID buttonID)
            {
                var buttonTextArrayIndex = Convert.ToInt32(buttonID);

                switch (languageID)
                {
                    case TwoLetterISOLanguageID.de: return BUTTON_TEXTS_GERMAN_DE[buttonTextArrayIndex];
                    case TwoLetterISOLanguageID.es: return BUTTON_TEXTS_SPANISH_ES[buttonTextArrayIndex];
                    case TwoLetterISOLanguageID.it: return BUTTON_TEXTS_ITALIAN_IT[buttonTextArrayIndex];

                    default: return BUTTON_TEXTS_ENGLISH_EN[buttonTextArrayIndex];
                }
            }

            static double GetCorrectedWorkingAreaFactor(double workingAreaFactor)
            {
                const double MIN_FACTOR = 0.2;
                const double MAX_FACTOR = 1.0;

                if (workingAreaFactor < MIN_FACTOR)
                {
                    return MIN_FACTOR;
                }

                if (workingAreaFactor > MAX_FACTOR)
                {
                    return MAX_FACTOR;
                }

                return workingAreaFactor;
            }

            static void SetDialogStartPosition(FlexibleMessageBoxForm flexibleMessageBoxForm, IWin32Window owner)
            {
                //If no owner given: Center on current screen
                if (owner == null)
                {
                    var screen = Screen.FromPoint(Cursor.Position);
                    flexibleMessageBoxForm.StartPosition = FormStartPosition.Manual;
                    flexibleMessageBoxForm.Left = screen.Bounds.Left + screen.Bounds.Width / 2 - flexibleMessageBoxForm.Width / 2;
                    flexibleMessageBoxForm.Top = screen.Bounds.Top + screen.Bounds.Height / 2 - flexibleMessageBoxForm.Height / 2;
                }
            }

            static void SetDialogSizes(FlexibleMessageBoxForm flexibleMessageBoxForm, string text, string caption)
            {
                //First set the bounds for the maximum dialog size
                flexibleMessageBoxForm.MaximumSize = new Size(Convert.ToInt32(SystemInformation.WorkingArea.Width * FlexibleMessageBoxForm.GetCorrectedWorkingAreaFactor(MAX_WIDTH_FACTOR)),
                                                              Convert.ToInt32(SystemInformation.WorkingArea.Height * FlexibleMessageBoxForm.GetCorrectedWorkingAreaFactor(MAX_HEIGHT_FACTOR)));

                //Get rows. Exit if there are no rows to render...
                var stringRows = GetStringRows(text);
                if (stringRows == null)
                {
                    return;
                }

                //Calculate whole text height
                var textHeight = TextRenderer.MeasureText(text, FONT).Height;

                //Calculate width for longest text line
                const int SCROLLBAR_WIDTH_OFFSET = 15;
                var longestTextRowWidth = stringRows.Max(textForRow => TextRenderer.MeasureText(textForRow, FONT).Width);
                var captionWidth = TextRenderer.MeasureText(caption, SystemFonts.CaptionFont).Width;
                var textWidth = Math.Max(longestTextRowWidth + SCROLLBAR_WIDTH_OFFSET, captionWidth);

                //Calculate margins
                var marginWidth = flexibleMessageBoxForm.Width - flexibleMessageBoxForm.richTextBoxMessage.Width;
                var marginHeight = flexibleMessageBoxForm.Height - flexibleMessageBoxForm.richTextBoxMessage.Height;

                //Set calculated dialog size (if the calculated values exceed the maximums, they were cut by windows forms automatically)
                flexibleMessageBoxForm.Size = new Size(textWidth + marginWidth,
                                                       textHeight + marginHeight);
            }

            static void SetDialogIcon(FlexibleMessageBoxForm flexibleMessageBoxForm, MessageBoxIcon icon)
            {
                switch (icon)
                {
                    case MessageBoxIcon.Information:
                        flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Information.ToBitmap();
                        break;
                    case MessageBoxIcon.Warning:
                        flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Warning.ToBitmap();
                        break;
                    case MessageBoxIcon.Error:
                        flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Error.ToBitmap();
                        break;
                    case MessageBoxIcon.Question:
                        flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Question.ToBitmap();
                        break;
                    default:
                        //When no icon is used: Correct placement and width of rich text box.
                        flexibleMessageBoxForm.pictureBoxForIcon.Visible = false;
                        flexibleMessageBoxForm.richTextBoxMessage.Left -= flexibleMessageBoxForm.pictureBoxForIcon.Width;
                        flexibleMessageBoxForm.richTextBoxMessage.Width += flexibleMessageBoxForm.pictureBoxForIcon.Width;
                        break;
                }
            }

            static void SetDialogButtons(FlexibleMessageBoxForm flexibleMessageBoxForm, MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton)
            {
                //Set the buttons visibilities and texts
                switch (buttons)
                {
                    case MessageBoxButtons.AbortRetryIgnore:
                        flexibleMessageBoxForm.visibleButtonsCount = 3;

                        flexibleMessageBoxForm.button1.Visible = true;
                        flexibleMessageBoxForm.button1.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.ABORT);
                        flexibleMessageBoxForm.button1.DialogResult = DialogResult.Abort;

                        flexibleMessageBoxForm.button2.Visible = true;
                        flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.RETRY);
                        flexibleMessageBoxForm.button2.DialogResult = DialogResult.Retry;

                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.IGNORE);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.Ignore;

                        flexibleMessageBoxForm.ControlBox = false;
                        break;

                    case MessageBoxButtons.OKCancel:
                        flexibleMessageBoxForm.visibleButtonsCount = 2;

                        flexibleMessageBoxForm.button2.Visible = true;
                        flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.OK);
                        flexibleMessageBoxForm.button2.DialogResult = DialogResult.OK;

                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.CANCEL);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;

                        flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
                        break;

                    case MessageBoxButtons.RetryCancel:
                        flexibleMessageBoxForm.visibleButtonsCount = 2;

                        flexibleMessageBoxForm.button2.Visible = true;
                        flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.RETRY);
                        flexibleMessageBoxForm.button2.DialogResult = DialogResult.Retry;

                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.CANCEL);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;

                        flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
                        break;

                    case MessageBoxButtons.YesNo:
                        flexibleMessageBoxForm.visibleButtonsCount = 2;

                        flexibleMessageBoxForm.button2.Visible = true;
                        flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.YES);
                        flexibleMessageBoxForm.button2.DialogResult = DialogResult.Yes;

                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.NO);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.No;

                        flexibleMessageBoxForm.ControlBox = false;
                        break;

                    case MessageBoxButtons.YesNoCancel:
                        flexibleMessageBoxForm.visibleButtonsCount = 3;

                        flexibleMessageBoxForm.button1.Visible = true;
                        flexibleMessageBoxForm.button1.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.YES);
                        flexibleMessageBoxForm.button1.DialogResult = DialogResult.Yes;

                        flexibleMessageBoxForm.button2.Visible = true;
                        flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.NO);
                        flexibleMessageBoxForm.button2.DialogResult = DialogResult.No;

                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.CANCEL);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;

                        flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
                        break;

                    case MessageBoxButtons.OK:
                    default:
                        flexibleMessageBoxForm.visibleButtonsCount = 1;
                        flexibleMessageBoxForm.button3.Visible = true;
                        flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.OK);
                        flexibleMessageBoxForm.button3.DialogResult = DialogResult.OK;

                        flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
                        break;
                }

                //Set default button (used in FlexibleMessageBoxForm_Shown)
                flexibleMessageBoxForm.defaultButton = defaultButton;
            }

            #endregion

            #region Private event handlers

            void FlexibleMessageBoxForm_Shown(object sender, EventArgs e)
            {
                int buttonIndexToFocus = 1;
                Button buttonToFocus;

                //Set the default button...
                switch (defaultButton)
                {
                    case MessageBoxDefaultButton.Button1:
                    default:
                        buttonIndexToFocus = 1;
                        break;
                    case MessageBoxDefaultButton.Button2:
                        buttonIndexToFocus = 2;
                        break;
                    case MessageBoxDefaultButton.Button3:
                        buttonIndexToFocus = 3;
                        break;
                }

                if (buttonIndexToFocus > visibleButtonsCount)
                {
                    buttonIndexToFocus = visibleButtonsCount;
                }

                if (buttonIndexToFocus == 3)
                {
                    buttonToFocus = button3;
                }
                else if (buttonIndexToFocus == 2)
                {
                    buttonToFocus = button2;
                }
                else
                {
                    buttonToFocus = button1;
                }

                buttonToFocus.Focus();
            }

            void LinkClicked(object sender, LinkClickedEventArgs e)
            {
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    Process.Start(e.LinkText);
                }
                catch (Exception)
                {
                    //Let the caller of FlexibleMessageBoxForm decide what to do with this exception...
                    throw;
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }

            void FlexibleMessageBoxForm_KeyUp(object sender, KeyEventArgs e)
            {
                //Handle standard key strikes for clipboard copy: "Ctrl + C" and "Ctrl + Insert"
                if (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.Insert))
                {
                    var buttonsTextLine = (button1.Visible ? button1.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty)
                                        + (button2.Visible ? button2.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty)
                                        + (button3.Visible ? button3.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty);

                    //Build same clipboard text like the standard .Net MessageBox
                    var textForClipboard = STANDARD_MESSAGEBOX_SEPARATOR_LINES
                                         + Text + Environment.NewLine
                                         + STANDARD_MESSAGEBOX_SEPARATOR_LINES
                                         + richTextBoxMessage.Text + Environment.NewLine
                                         + STANDARD_MESSAGEBOX_SEPARATOR_LINES
                                         + buttonsTextLine.Replace("&", string.Empty) + Environment.NewLine
                                         + STANDARD_MESSAGEBOX_SEPARATOR_LINES;

                    //Set text in clipboard
                    Clipboard.SetText(textForClipboard);
                }
            }

            #endregion

            #region Properties (only used for binding)

            public string CaptionText { get; set; }
            public string MessageText { get; set; }

            #endregion

            #region Public show function

            public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            {
                //Create a new instance of the FlexibleMessageBox form
                var flexibleMessageBoxForm = new FlexibleMessageBoxForm
                {
                    ShowInTaskbar = false,

                    //Bind the caption and the message text
                    CaptionText = caption,
                    MessageText = text
                };
                flexibleMessageBoxForm.FlexibleMessageBoxFormBindingSource.DataSource = flexibleMessageBoxForm;

                //Set the buttons visibilities and texts. Also set a default button.
                SetDialogButtons(flexibleMessageBoxForm, buttons, defaultButton);

                //Set the dialogs icon. When no icon is used: Correct placement and width of rich text box.
                SetDialogIcon(flexibleMessageBoxForm, icon);

                //Set the font for all controls
                flexibleMessageBoxForm.Font = FONT;
                flexibleMessageBoxForm.richTextBoxMessage.Font = FONT;

                //Calculate the dialogs start size (Try to auto-size width to show longest text row). Also set the maximum dialog size. 
                SetDialogSizes(flexibleMessageBoxForm, text, caption);

                //Set the dialogs start position when given. Otherwise center the dialog on the current screen.
                SetDialogStartPosition(flexibleMessageBoxForm, owner);

                //Show the dialog
                return flexibleMessageBoxForm.ShowDialog(owner);
            }

            #endregion
        } //class FlexibleMessageBoxForm

        #endregion
    }
}
