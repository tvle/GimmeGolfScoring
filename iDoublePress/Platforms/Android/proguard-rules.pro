# Example ProGuard/R8 rules for the MAUI app
# Keep app entry points and anything R8 shouldn't obfuscate.
# Add or adjust rules if you see missing types at runtime.

# Keep the application class if present
-keep class com.microsoft.maui.MauiApplication { *; }

# Keep generated resource classes
-keep class **.R { *; }

# Keep any types used by reflection (adjust as needed)
-keepclassmembers class * {
    public <init>(...);
}

# Keep Xamarin / MAUI related types used via reflection
-keep class mono.android.** { *; }
-keep class android.** { *; }

# Keep custom renderers or platform-specific wrappers
-keep class com.idoublepress.** { *; }
