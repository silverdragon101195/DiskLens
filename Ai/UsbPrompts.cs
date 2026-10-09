using System.Globalization;
using System.Text;
using SysLens.ViewModels;

namespace SysLens.Ai;

/// <summary>
/// Prompts for asking about a USB device or hub. They carry names, VID:PID, port locations, status and
/// the hub chain, never instance ids, which embed device serial numbers.
/// </summary>
public static class UsbPrompts
{
    private const string System =
        """
        You are a Windows USB expert helping a user diagnose devices and hubs. The user shows you one
        node from the USB device tree: what the device reports about itself, its Plug and Play status,
        and the chain of hubs between it and the root hub of the host controller.

        Rules:
        - Always answer in Vietnamese, concise, formatted as Markdown (headings, bullet lists, `code`).
        - Identify the vendor and product from the VID:PID and the reported name. If you are not sure
          what a VID:PID is, say so instead of guessing.
        - Judge the topology: USB allows at most 5 hubs between the root hub and a device. Monitors,
          docks and keyboards often contain hidden internal hubs that count as tiers, and devices near
          the limit tend to drop off or fail to enumerate (Device Descriptor Request Failed, code 43).
          Mixed USB 2 and USB 3 hubs, bus-powered hubs and long or passive cables make this worse.
        - Explain the Device Manager problem code when there is one.
        - Give concrete fixes in order of likelihood: plug the device or its hub chain closer to the
          root port, remove hub tiers, use a self-powered hub, swap cables, disable USB selective
          suspend, update chipset, hub or device firmware.
        """;

    private const string QuestionDisplay =
        "What is this device, is its place in the USB tree or its status a problem, and how do I fix it?";

    public static AssistantTopic ForDevice(UsbDeviceItem item)
    {
        var subject = item.VidPid.Length > 0 ? $"{item.Name} · {item.VidPid}" : item.Name;
        return new AssistantTopic(subject, System, Question(item), QuestionDisplay);
    }

    private static string Question(UsbDeviceItem item)
    {
        var text = new StringBuilder();
        text.AppendLine(Describe(item));
        text.AppendLine("Answer with exactly these three sections:");
        text.AppendLine("1. What this device is (vendor, product, role; for a hub, what it is likely built into).");
        text.AppendLine("2. Whether its position in the hub chain or its status is a problem, and why.");
        text.AppendLine("3. What to do about it, step by step. If nothing is wrong, say so briefly.");
        return text.ToString();
    }

    private static string Describe(UsbDeviceItem item)
    {
        var text = new StringBuilder();
        var kind = item.IsRootHub ? "Root hub" : item.IsHub ? "Hub" : "Device";
        text.AppendLine($"{kind}: {Label(item)}");
        text.AppendLine($"Status: {item.StatusText}");
        if (item.Location.Length > 0)
            text.AppendLine($"Location: {item.Location}");

        if (!item.IsChainKnown)
        {
            text.AppendLine("The hub chain up to the root hub is unknown: Windows no longer has a record of a "
                            + "disconnected hub above this node. The chain below lists only the known part.");
        }
        else if (!item.IsRootHub)
        {
            text.AppendLine($"External hubs between the root hub and this node (counting itself if it is a hub): "
                            + $"{item.HubDepth} of at most {UsbDeviceItem.MaxExternalHubs}");
        }

        var chain = item.HubChain;
        if (chain.Count > 0)
        {
            text.AppendLine(item.IsChainKnown ? "Hub chain from the root hub down:" : "Known part of the hub chain, top down:");
            for (var i = 0; i < chain.Count; i++)
                text.AppendLine($"{i + 1}. {Label(chain[i])}{TierNote(chain[i])}");
        }

        if (!item.IsPresent)
        {
            text.AppendLine("The device is not connected now.");
            text.AppendLine($"History: {item.HistoryText}");
        }

        if (item.Children.Count > 0)
        {
            text.AppendLine("Connected directly to this hub:");
            foreach (var child in item.Children)
                text.AppendLine($"- {Label(child)}, {child.StatusText}{(child.Location.Length > 0 ? $", {child.Location}" : "")}");
        }

        return text.ToString();
    }

    private static string Label(UsbDeviceItem item) =>
        item.VidPid.Length > 0 ? $"{item.Name} ({item.VidPid})" : item.Name;

    private static string TierNote(UsbDeviceItem hub) =>
        hub.IsRootHub ? " [root hub]"
        : hub.IsHub && hub.IsChainKnown ? string.Create(CultureInfo.InvariantCulture, $" [hub tier {hub.HubDepth}]")
        : "";
}
