using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DTCL.Common
{
    public class ButtonManager
    {
        List<Button> _buttons;
        Button _exitButton;
        Button _loopBackButton;
        static ButtonManager _instance;
        static readonly object _lockObject = new object();

        public static ButtonManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                            _instance = new ButtonManager();
                    }
                }

                return _instance;
            }
        }

        private ButtonManager() { }

        public void InitButtonManager(List<Button> buttons, Button exitButton, Button loopBackButton)
        {
            _buttons = buttons;
            _exitButton = exitButton;
            _loopBackButton = loopBackButton;
        }

        public void SetButtonColorState(Button buttonToActivate, Color color)
        {
            foreach (var button in _buttons)
            {
                button.IsEnabled = false;
                button.Background = new SolidColorBrush(Colors.DarkGray);
            }

            buttonToActivate.Background = new SolidColorBrush(color);
            _exitButton.IsEnabled = true; // Keep exit enabled
        }

        public void SetOnlyButtonColorState(Button buttonToActivate, Color color)
        {
            buttonToActivate.Background = new SolidColorBrush(color);
            buttonToActivate.IsEnabled = false;
        }

        public void ResetOnlyButtonColorState(Button buttonToActivate, Color color)
        {
            buttonToActivate.Background = new SolidColorBrush(color);
            buttonToActivate.IsEnabled = true;
        }

        public void ResetButtonColorStates(Color defaultColor)
        {
            foreach (var button in _buttons)
            {
                button.IsEnabled = true;
                button.Background = new SolidColorBrush(defaultColor);
            }
        }

        public void ShowOnlyButtons(List<Button> buttonsToShow)
        {
            foreach (var button in _buttons)
            {
                button.Visibility = buttonsToShow.Contains(button) ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public void ShowOrHideOnlyListButtons(List<Button> buttonsToShow, bool state)
        {
            foreach (var button in buttonsToShow)
                button.Visibility = state ? Visibility.Visible : Visibility.Hidden;
        }

        public void DisableOnlyButtons(List<Button> buttonsToDisable)
        {
            foreach (var button in _buttons)
                button.IsEnabled = !buttonsToDisable.Contains(button);
        }

        public void ShowOnlyExitAtStart() => ShowOnlyButtons(new List<Button> { _exitButton });

        public void ShowAllButtonsExcept(params string[] buttonNamesToExclude)
        {
            foreach (var button in _buttons)
            {
                button.Visibility = buttonNamesToExclude.Contains(button.Name) ? Visibility.Hidden : Visibility.Visible;
            }
        }

        public void HandleKeyDown(KeyEventArgs e, ref bool isPCMode)
        {
            var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (ctrl && e.Key == Key.P)
            {
                isPCMode = true;

                ShowOnlyButtons(new List<Button>
                {
                    _exitButton,
                    _buttons.Find(b => b.Name == "PerformanceCheck")
                });
            }
            else if (ctrl && e.Key == Key.U)
            {
                isPCMode = false;

                ShowOnlyButtons(new List<Button>
                {
                    _exitButton,
                    _buttons.Find(b => b.Name == "Utility")
                });
            }
            else if (ctrl && e.Key == Key.A)
            {
                isPCMode = false;

                ShowOnlyButtons(new List<Button>
                {
                    _exitButton,
                    _loopBackButton,
                    _buttons.Find(b => b.Name == "AppButton")
                });
            }
        }
    }
}