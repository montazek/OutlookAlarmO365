using System.Reflection;
using GarageKept.OutlookAlarm.Alarm.UI.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageKept.OutlookAlarm.Alarm.Tests;

[TestClass]
public class SettingsFormTests
{
    private static readonly MethodInfo ClampToRange = typeof(SettingsForm).GetMethod(
        "ClampToRange", BindingFlags.NonPublic | BindingFlags.Static)!;

    [DataTestMethod]
    [DataRow(2560, 0, 1600, 1600)]
    [DataRow(-50, 0, 1600, 0)]
    [DataRow(800, 0, 1600, 800)]
    [DataRow(10, 1, 0, 1)]
    public void ClampToRange_AlwaysReturnsValidControlValue(
        int value, int minimum, int maximum, int expected)
    {
        var result = (int)ClampToRange.Invoke(null, new object[] { value, minimum, maximum })!;

        Assert.AreEqual(expected, result);
    }
}
