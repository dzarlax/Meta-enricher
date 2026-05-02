using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetaEnricher.Models;
using MetaEnricher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MetaEnricher.Views;

public sealed partial class FullscreenPage : Page
{
    public AppState AppState => AppState.Instance;
    private List<Photo> _photos = new();
    private int _currentIndex = 0;
    private int _rotationDegrees = 0;

    public FullscreenPage()
    {
        this.InitializeComponent();
        Loaded += FullscreenPage_Loaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _photos = AppState.Photos.ToList();

        if (e.Parameter is string filePath)
        {
            _currentIndex = _photos.FindIndex(p => p.FilePath == filePath);
            if (_currentIndex < 0) _currentIndex = 0;
        }
    }

    private async void FullscreenPage_Loaded(object sender, RoutedEventArgs e)
    {
        this.Focus(FocusState.Programmatic);
        await LoadCurrentPhoto();
    }

    private async System.Threading.Tasks.Task LoadCurrentPhoto()
    {
        if (_photos.Count == 0) return;
        if (_currentIndex < 0) _currentIndex = 0;
        if (_currentIndex >= _photos.Count) _currentIndex = _photos.Count - 1;

        _rotationDegrees = 0;
        ApplyRotation();

        var photo = _photos[_currentIndex];
        TbFilename.Text = photo.FileName;

        // Load full-res image
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(photo.FilePath);
            using var stream = await file.OpenReadAsync();
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);

            var transform = new Windows.Graphics.Imaging.BitmapTransform
            {
                ScaledWidth = decoder.PixelWidth,
                ScaledHeight = decoder.PixelHeight,
            };

            var softBitmap = await decoder.GetSoftwareBitmapAsync(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                transform,
                Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb);

            if (softBitmap.BitmapPixelFormat != Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8 ||
                softBitmap.BitmapAlphaMode != Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied)
            {
                softBitmap = Windows.Graphics.Imaging.SoftwareBitmap.Convert(
                    softBitmap,
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            }

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softBitmap);
            FullscreenImage.Source = source;
        }
        catch
        {
            // Fallback: load as BitmapImage
            var bmp = new BitmapImage(new Uri(photo.FilePath));
            FullscreenImage.Source = bmp;
        }

        // EXIF info
        var m = photo.Meta;
        LblDate.Text = m.DateTimeOriginal ?? "";
        LblCamera.Text = (m.Make != null || m.Model != null) ? $"{m.Make} {m.Model}".Trim() : "";
        LblFocal.Text = m.FocalLength ?? "";
        LblAperture.Text = m.Aperture ?? "";
        LblShutter.Text = m.ShutterSpeed ?? "";
        LblIso.Text = m.Iso.HasValue ? $"ISO {m.Iso}" : "";
    }

    private void ApplyRotation()
    {
        FullscreenImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        FullscreenImage.RenderTransform = new RotateTransform { Angle = _rotationDegrees };
    }

    private async void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            await LoadCurrentPhoto();
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _photos.Count - 1)
        {
            _currentIndex++;
            await LoadCurrentPhoto();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private void BtnRotateCW_Click(object sender, RoutedEventArgs e)
    {
        _rotationDegrees = (_rotationDegrees + 90) % 360;
        ApplyRotation();
    }

    private void BtnRotateCCW_Click(object sender, RoutedEventArgs e)
    {
        _rotationDegrees = (_rotationDegrees - 90 + 360) % 360;
        ApplyRotation();
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Check if Shift is held using the modifier keys on the event args
        bool shiftDown = (e.KeyStatus.IsExtendedKey == false) &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                Frame.GoBack();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Left:
                if (_currentIndex > 0) { _currentIndex--; await LoadCurrentPhoto(); }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Right:
                if (_currentIndex < _photos.Count - 1) { _currentIndex++; await LoadCurrentPhoto(); }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.R:
                if (shiftDown)
                    _rotationDegrees = (_rotationDegrees - 90 + 360) % 360;
                else
                    _rotationDegrees = (_rotationDegrees + 90) % 360;
                ApplyRotation();
                e.Handled = true;
                break;
        }
    }
}
