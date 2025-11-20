using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DTCL
{
    /// <summary>
    /// Interaction logic for HeaderInfoDisplay.xaml
    /// </summary>
    public partial class HeaderInfoDisplay : Window
    {
        public IMessageInfoContainer uMessageContainerObj;
        public JsonParser<UploadMessageInfoContainer> uMessageParserObj;
        string selectedPath = "";
        PopUpMessagesContainer PopUpMessagesContainerObj;
        CartType cartType;
        public HeaderInfoDisplay(CartType _cartType)
        {
            cartType = _cartType;
            InitializeComponent();
            var PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();
            PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");
        }

        private HeaderInfoDisplay()
        {
        }

        void Button_Click(object sender, object e)
        {
        }

        void UpdateItem_Click(object sender, object e)
        {
            var MessageParserObj = new JsonParser<UploadMessageInfoContainer>();

            var MessageContainerObj = MessageParserObj.Deserialize("D3\\D3UploadMessageDetails.json");

            HeaderInfo.UpdateMessageInfoWithHeaderData(cartType, "c:\\mps\\darin3\\upload\\", MessageContainerObj);

            UploadMessageInfoDataGrid.ItemsSource = MessageContainerObj.MessageInfoList;

            var serializedJson = MessageParserObj.Serialize(MessageContainerObj);
        }

        void AddItem_Click(object sender, RoutedEventArgs e)
        {
        }

        void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
        }

        void Browse_Click_1(object sender, RoutedEventArgs e)
        {
        }

        public bool InitializeUploadMessages(string msgPath)
        {
            if (cartType == CartType.Darin3)
            {
                uMessageContainerObj = new UploadMessageInfoContainer();

                uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

                uMessageContainerObj = uMessageParserObj.Deserialize("D3\\D3UploadMessageDetails.json");

                var res = HeaderInfo.UpdateMessageInfoWithHeaderData(cartType, msgPath, uMessageContainerObj);

                var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

                System.IO.File.WriteAllText("D3\\D3UploadMessageDetails.json", serializedJson);

                var collectionView = CollectionViewSource.GetDefaultView(uMessageContainerObj.MessageInfoList);
                // collectionView.Filter = item => ShouldDisplayRow(item);

                // Set the filtered CollectionView as the ItemsSource for the DataGrid
                UploadMessageInfoDataGrid.ItemsSource = collectionView;

                Log.Log.Info("Upload Messages Initialized");
                return res;
            }
            else
            {
                uMessageContainerObj = new UploadMessageInfoContainer();

                uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

                uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");

                var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, msgPath, uMessageContainerObj);

                var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

                System.IO.File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);

                var collectionView = CollectionViewSource.GetDefaultView(uMessageContainerObj.MessageInfoList);
                // collectionView.Filter = item => true;

                // Set the filtered CollectionView as the ItemsSource for the DataGrid
                UploadMessageInfoDataGrid.ItemsSource = collectionView;

                Log.Log.Info("Upload Messages Initialized");
                return res;
            }
        }

        bool ShouldDisplayRow(object item)
        {
            var messageInfo = item as UploadMessageInfo;

            if (messageInfo == null)
                return false;

            if (messageInfo.FileName.Contains("fpl") && (messageInfo.FileName.Any(char.IsDigit)))
                return false;
            else if (messageInfo.FileName.Contains("MonT2"))
                return false;

            return true;
        }

        void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true, // Allows selection of multiple files
                Filter = "Binary files (DR.bin)|DR.bin" // Adjust the filter as needed
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var selectedDRFile = openFileDialog.FileName;
                selectedPath = System.IO.Path.GetDirectoryName(selectedDRFile) + "\\";
                InitializeUploadMessages(selectedPath);
            }
        }

        void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!selectedPath.Equals(""))
            {
                if (WriteAllEditedMessagesToFile())
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Confirm_Msg"), this);
                    InitializeUploadMessages(selectedPath);
                }
                else
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Fail_Msg"), this);
                    InitializeUploadMessages(selectedPath);
                }
            }
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Missing_Msg3"), this);
            }
        }

        void Exit_Click(object sender, RoutedEventArgs e) => Close();

        void UploadMessageInfoDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
        }

        void UploadMessageInfoDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Check if the column being edited is the "NOB" column
            if (e.Column is DataGridTextColumn column && column.Header.ToString() == "NOB")
            {
                // Get the edited message info
                if (e.Row.Item is UploadMessageInfo editedMessage)
                {
                    // Access the TextBox being edited
                    var textBox = e.EditingElement as TextBox;

                    if (textBox != null)
                    {
                        // Get the new value entered by the user
                        if (int.TryParse(textBox.Text, out int newNobValue))
                        {
                            // Perform validation on the NOB value if required
                            if (newNobValue < 1 || newNobValue > editedMessage.MaxNob)
                            {
                                MessageBox.Show("NOB value must be between 1 and " + editedMessage.MaxNob.ToString(), "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                                e.Cancel = true; // Cancel the edit if validation fails
                            }
                            else
                            {
                                // Update the NOB value and take any further actions
                                editedMessage.Nob = newNobValue;
                                // MessageBox.Show($"NOB updated for File: {editedMessage.FileName}, New Value: {newNobValue}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid integer value for NOB.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                            e.Cancel = true; // Cancel the edit if the value is not valid
                        }
                    }
                }
            }
        }

        bool WriteAllEditedMessagesToFile()
        {
            if (UploadMessageInfoDataGrid.ItemsSource is ICollectionView collectionView)
            {
                int[] headerWords;

                if (cartType == CartType.Darin3)
                {
                    headerWords = HeaderInfo.ReadHeaderFileWords(selectedPath);
                }
                else
                {
                    headerWords = HeaderInfo.ReadD2HeaderFileWords(selectedPath);
                }

                // Iterate over each message and update header words
                foreach (var item in collectionView)
                {
                    if (item is UploadMessageInfo message)
                    {
                        if (!message.FileName.Contains("fpl"))
                        {
                            // Update NOB and defined header status for each message
                            HeaderInfo.setMessageNOBToHeader(ref headerWords, message);
                            HeaderInfo.SetIsDefinedInHeader(ref headerWords, message);
                        }
                        else if (message.FileName.Contains("fpl") && (message.FileName.Any(char.IsDigit)))
                        {
                            HeaderInfo.SetIsFPLDefinedInHeader(ref headerWords, message);
                            HeaderInfo.SetFPLNob(ref headerWords, message);
                        }
                    }
                }

                // Write the modified header words back to the file
                var res = HeaderInfo.WriteHeaderFileWords(cartType, selectedPath, headerWords);

                // Log the successful update
                if (res)
                    Log.Log.Info("All edited messages have been written to the header file.");

                return res;
            }

            return false;
        }

        void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;

            if (checkBox != null)
            {
                // Get the associated message by accessing the DataContext of the CheckBox
                var messageInfo = checkBox.DataContext as UploadMessageInfo;

                if (messageInfo != null)
                {
                    // MessageBox.Show($"Checkbox checked for File: {messageInfo.FileName}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}