using Android.App;
using Android.Views;
using Android.Widget;
using LeanAI.Application.ActivityTracking.Commands.ImportRun;
using LeanAI.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace LeanAI.Maui.Platforms.Android;

[Activity(Label = "Import Run", Exported = true)]
[IntentFilter(
    new[] { global::Android.Content.Intent.ActionSend },
    Categories = new[] { global::Android.Content.Intent.CategoryDefault },
    DataMimeType = "image/*",
    Label = "Import Run")]
public class ImportRunActivity : Activity
{
    private const string GeminiKeyStorageKey = "gemini_key";

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetLoadingView();

#pragma warning disable CA1422 // GetParcelableExtra(string) is deprecated on API 33+ but still functional on older versions
        var imageUri = Intent?.GetParcelableExtra(
            global::Android.Content.Intent.ExtraStream) as global::Android.Net.Uri;
#pragma warning restore CA1422

        if (imageUri is null)
        {
            Finish();
            return;
        }

        var mimeType = ContentResolver!.GetType(imageUri) ?? "image/jpeg";

        byte[] imageBytes;
        using (var stream = ContentResolver.OpenInputStream(imageUri)!)
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            imageBytes = ms.ToArray();
        }

        var services = IPlatformApplication.Current!.Services;
        var mediator = services.GetRequiredService<IMediator>();
        var date     = DateOnly.FromDateTime(DateTime.Today);

        Task.Run(async () =>
        {
            try
            {
                var apiKey = await SecureStorage.GetAsync(GeminiKeyStorageKey);
                if (!string.IsNullOrEmpty(apiKey))
                    services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;

                var metrics = await mediator.Send(
                    new ImportRunCommand(imageBytes, mimeType, date));

                var distance = metrics.FirstOrDefault(m => m.ParameterName == "distance");
                var duration = metrics.FirstOrDefault(m => m.ParameterName == "duration");

                RunOnUiThread(() =>
                    Toast.MakeText(
                            this,
                            $"Run imported: {distance?.Value} {distance?.Unit} in {duration?.Value}.",
                            ToastLength.Short)!
                         .Show());
            }
            catch
            {
                RunOnUiThread(() =>
                    Toast.MakeText(
                            this,
                            "Could not extract run data — please try again.",
                            ToastLength.Long)!
                         .Show());
            }
            finally
            {
                RunOnUiThread(Finish);
            }
        });
    }

    private void SetLoadingView()
    {
        var dp = Resources!.DisplayMetrics!.Density;

        // LeanAI design palette
        var colorBase   = global::Android.Graphics.Color.ParseColor("#222222");
        var colorText   = global::Android.Graphics.Color.ParseColor("#F0F2F5");
        var colorCopper = global::Android.Graphics.Color.ParseColor("#D28B5C");
        var colorNickel = global::Android.Graphics.Color.ParseColor("#9A9EAB");

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetGravity(GravityFlags.Center);
        root.SetBackgroundColor(colorBase);
        root.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        var appName = new TextView(this)
        {
            Text     = "LeanAI",
            TextSize = 26f,
            Gravity  = GravityFlags.Center
        };
        appName.SetTextColor(colorText);
        appName.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
        var nameLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        nameLp.BottomMargin = (int)(32 * dp);
        root.AddView(appName, nameLp);

        var spinner = new global::Android.Widget.ProgressBar(this, null,
            global::Android.Resource.Attribute.ProgressBarStyleLarge)
        {
            Indeterminate = true
        };
        spinner.IndeterminateTintList =
            global::Android.Content.Res.ColorStateList.ValueOf(colorCopper);
        var spinnerLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        spinnerLp.BottomMargin = (int)(20 * dp);
        root.AddView(spinner, spinnerLp);

        var label = new TextView(this)
        {
            Text     = "Analysing your run…",
            TextSize = 15f,
            Gravity  = GravityFlags.Center
        };
        label.SetTextColor(colorNickel);
        root.AddView(label);

        SetContentView(root);
    }
}
