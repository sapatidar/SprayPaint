using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows.Shapes;
using System.Linq;

namespace SprayPaint
{
    public partial class MainWindow : Window
    {
        private Color _currentColor = Colors.Black; // Default brush color
        private int _brushSize = 10; // Default brush size
        private int _density = 5; // Default spray density
        private Border _selectedColorBox; // Keeps track of selected color box
        private bool _isEraserMode = false;
        private int _eraserSize = 10;

        public MainWindow()
        {
            InitializeComponent();

            // Attach event handlers
            BrushSizeSlider.ValueChanged += BrushSizeSlider_ValueChanged;
            DensitySlider.ValueChanged += DensitySlider_ValueChanged;
            EraserSizeSlider.ValueChanged += EraserSizeSlider_ValueChanged;
        }

        // Loads an image onto the canvas and clears previous drawings
        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                BitmapImage bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                LoadedImage.Source = bitmap;
                SprayCanvas.Children.Clear();
            }
        }

        // Saves the canvas (image and drawings) to a PNG file
        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Image Files|*.png"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var renderTarget = new RenderTargetBitmap((int)SprayCanvas.ActualWidth, (int)SprayCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    // Draw the original image
                    if (LoadedImage.Source is BitmapSource bitmapSource)
                    {
                        context.DrawImage(bitmapSource, new Rect(0, 0, SprayCanvas.ActualWidth, SprayCanvas.ActualHeight));
                    }
                    // Draw the spray paint
                    VisualBrush brush = new VisualBrush(SprayCanvas);
                    context.DrawRectangle(brush, null, new Rect(new Point(), new Size(SprayCanvas.ActualWidth, SprayCanvas.ActualHeight)));
                }
                renderTarget.Render(visual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                using (FileStream file = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(file);
                }
            }
        }

        // Clears all drawings from the canvas
        private void ClearCanvas_Click(object sender, RoutedEventArgs e)
        {
            SprayCanvas.Children.Clear();
        }

        // Updates the brush size based on slider value
        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _brushSize = (int)BrushSizeSlider.Value;
        }

        // Updates the spray density based on slider value
        private void DensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _density = (int)DensitySlider.Value;
        }

        // Updates the eraser size based on slider value
        private void EraserSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _eraserSize = (int)EraserSizeSlider.Value;
        }

        // Updates the selected color and highlights the corresponding color box
        private void ColorBox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                // Update the current color
                _currentColor = ((SolidColorBrush)border.Background).Color;
                // Highlight the selected color box
                if (_selectedColorBox != null)
                {
                    _selectedColorBox.BorderBrush = Brushes.Gray;
                }
                _selectedColorBox = border;
                _selectedColorBox.BorderBrush = Brushes.White;
            }
        }

        // Handles spray painting and erasing based on the mouse movement
        private void SprayCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                if (_isEraserMode)
                {
                    var elementsToRemove = SprayCanvas.Children
                        .Cast<UIElement>()
                        .Where(child =>
                        {
                            double childX = Canvas.GetLeft(child) + ((FrameworkElement)child).ActualWidth / 2;
                            double childY = Canvas.GetTop(child) + ((FrameworkElement)child).ActualHeight / 2;
                            double mouseX = e.GetPosition(SprayCanvas).X;
                            double mouseY = e.GetPosition(SprayCanvas).Y;
                            // Check if the child is within the eraser's radius
                            return Math.Sqrt(Math.Pow(childX - mouseX, 2) + Math.Pow(childY - mouseY, 2)) <= _eraserSize / 2;
                        })
                        .ToList();
                    foreach (var element in elementsToRemove)
                    {
                        SprayCanvas.Children.Remove(element);
                    }
                }
                else
                {
                    var random = new Random();
                    for (int i = 0; i < _density; i++)
                    {
                        double offsetX = random.NextDouble() * _brushSize - (_brushSize / 2);
                        double offsetY = random.NextDouble() * _brushSize - (_brushSize / 2);
                        Ellipse dot = new Ellipse
                        {
                            Width = _brushSize / 5.0,
                            Height = _brushSize / 5.0,
                            Fill = new SolidColorBrush(_currentColor)
                        };
                        Canvas.SetLeft(dot, e.GetPosition(SprayCanvas).X + offsetX);
                        Canvas.SetTop(dot, e.GetPosition(SprayCanvas).Y + offsetY);
                        SprayCanvas.Children.Add(dot);
                    }
                }
            }
        }

        // Toggles eraser mode and updates the UI
        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            _isEraserMode = !_isEraserMode;
            // Update button content for clarity
            EraserButton.Content = _isEraserMode ? "Paint" : "Eraser";
            EraserSliderContainer.Visibility = _isEraserMode ? Visibility.Visible : Visibility.Collapsed;
            // Optional: Change cursor to indicate eraser mode
            SprayCanvas.Cursor = _isEraserMode ? System.Windows.Input.Cursors.Cross : System.Windows.Input.Cursors.Pen;
        }

        // Saves the positions and colors of spray dots to a text file
        private void SaveSprayChanges_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files|*.txt"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                {
                    foreach (UIElement element in SprayCanvas.Children)
                    {
                        if (element is Ellipse dot)
                        {
                            double left = Canvas.GetLeft(dot);
                            double top = Canvas.GetTop(dot);
                            double size = dot.Width;
                            string color = ((SolidColorBrush)dot.Fill).Color.ToString();
                            writer.WriteLine($"{left},{top},{size},{color}");
                        }
                    }
                }
            }
        }

        // Loads spray dot data from a text file and re-draws them on the canvas
        private void LoadSprayChanges_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files|*.txt"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                using (StreamReader reader = new StreamReader(openFileDialog.FileName))
                {
                    SprayCanvas.Children.Clear();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 4 &&
                            double.TryParse(parts[0], out double left) &&
                            double.TryParse(parts[1], out double top) &&
                            double.TryParse(parts[2], out double size))
                        {
                            Color color = (Color)ColorConverter.ConvertFromString(parts[3]);
                            Ellipse dot = new Ellipse
                            {
                                Width = size,
                                Height = size,
                                Fill = new SolidColorBrush(color)
                            };
                            Canvas.SetLeft(dot, left);
                            Canvas.SetTop(dot, top);
                            SprayCanvas.Children.Add(dot);
                        }
                    }
                }
            }
        }
    }
}
