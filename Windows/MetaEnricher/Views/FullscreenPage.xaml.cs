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

        // KeyboardAccelerator works regardless of focus — reliable for Escape
        var esc = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Escape
        };
        esc.Invoked += (_, e) => { Frame.GoBack(); e.Handled = true; };
        KeyboardAccelerators.Add(esc);
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

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ReleaseCurrentImage();
        // Decoded WinRT pixel buffers don't always release on GC's first pass — be explicit
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private async void FullscreenPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCurrentPhoto();
        this.Focus(FocusState.Programmatic);
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

        // Release previous image first
        ReleaseCurrentImage();

        if (!System.IO.File.Exists(photo.FilePath))
        {
            FullscreenImage.Source = null;
            return;
        }

        // Load image scaled to the viewport, not full resolution.
        // 24MP at full res = ~96 MB per photo — that's how memory blew up to 2 GB.
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(photo.FilePath);
            using var stream = await file.OpenReadAsync();
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);

            // Cap at 2× viewport size to allow some zoom-in headroom while keeping memory sane.
            uint maxDim = (uint)Math.Max(1024, Math.Min(3840, this.ActualWidth * 2));
            uint origW = decoder.PixelWidth;
            uint origH = decoder.PixelHeight;
            double scale = Math.Min(1.0, (double)maxDim / Math.Max(origW, origH));

            var transform = new Windows.Graphics.Imaging.BitmapTransform
            {
                ScaledWidth = (uint)(origW * scale),
                ScaledHeight = (uint)(origH * scale),
                InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant,
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
                var converted = Windows.Graphics.Imaging.SoftwareBitmap.Convert(
                    softBitmap,
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                softBitmap.Dispose();
                softBitmap = converted;
            }

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softBitmap);
            softBitmap.Dispose();
            FullscreenImage.Source = source;
        }
        catch (System.IO.FileNotFoundException)
        {
            FullscreenImage.Source = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Fullscreen] Decode failed for {photo.FilePath}: {ex.Message} — falling back to BitmapImage");
            FullscreenImage.Source = new BitmapImage(new Uri(photo.FilePath));
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

    private void ReleaseCurrentImage()
    {
        if (FullscreenImage.Source is SoftwareBitmapSource sbs)
        {
            try { sbs.Dispose(); } catch { }
        }
        FullscreenImage.Source = null;
    }

    private void ApplyRotation()
    {
        FullscreenImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        FullscreenImage.RenderTransform = new RotateTransform { Angle = _rotationDegrees };
    }

    private async void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0) { _currentIndex--; await LoadCurrentPhoto(); }
        this.Focus(FocusState.Programmatic);
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _photos.Count - 1) { _currentIndex++; await LoadCurrentPhoto(); }
        this.Focus(FocusState.Programmatic);
    }

    private void FullscreenImage_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        this.Focus(FocusState.Programmatic);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private void BtnRotateCW_Click(object sender, RoutedEventArgs e)
    {
        _rotationDegrees = (_rotationDegrees + 90) % 360;
        ApplyRotation();
        this.Focus(FocusState.Programmatic);
    }

    private void BtnRotateCCW_Click(object sender, RoutedEventArgs e)
    {
        _rotationDegrees = (_rotationDegrees - 90 + 360) % 360;
        ApplyRotation();
        this.Focus(FocusState.Programmatic);
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Check if Shift is held using the modifier keys on the event args
        bool shiftDown = (e.KeyStatus.IsExtendedKey == false) &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
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
