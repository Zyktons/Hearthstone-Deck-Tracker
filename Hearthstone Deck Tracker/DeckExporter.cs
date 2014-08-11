﻿#region

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hearthstone_Deck_Tracker.Hearthstone;

#endregion

namespace Hearthstone_Deck_Tracker
{
	internal static class DeckExporter
	{
		public static async Task Export(Deck deck)
		{
			if(deck == null) return;

			var hsHandle = User32.FindWindow("UnityWndClass", "Hearthstone");

			if(!User32.IsForegroundWindow("Hearthstone"))
			{
				//restore window and bring to foreground
				User32.ShowWindow(hsHandle, User32.SwRestore);
				User32.SetForegroundWindow(hsHandle);
				//wait it to actually be in foreground, else the rect might be wrong
				await Task.Delay(500);
			}
			if(!User32.IsForegroundWindow("Hearthstone"))
			{
				MessageBox.Show("Can't find Heartstone window.");
				return;
			}

			var hsRect = User32.GetHearthstoneRect(false);
			var bounds = Screen.FromHandle(hsHandle).Bounds;
			var ratio = (4.0 / 3.0) / ((double)hsRect.Width / hsRect.Height);

			if(Config.Instance.ExportSetDeckName)
				await SetDeckName(deck.Name, ratio, hsRect.Width, hsRect.Height, hsHandle);

			await ClickAllCrystal(ratio, hsRect.Width, hsRect.Height, hsHandle);

			foreach(var card in deck.Cards)
				await AddCardToDeck(card, ratio, hsRect.Width, hsRect.Height, hsHandle);
		}

		private static async Task ClickAllCrystal(double ratio, int width, int height, IntPtr hsHandle)
		{
			await ClickOnPoint(hsHandle, new Point((int)GetXPos(Config.Instance.ExportAllButtonX, width, ratio), (int)(Config.Instance.ExportAllButtonY * height)));
		}

		private static async Task SetDeckName(string name, double ratio, int width, int height, IntPtr hsHandle)
		{
			var nameDeckPos = new Point((int)GetXPos(Config.Instance.ExportNameDeckX, width, ratio), (int)(Config.Instance.ExportNameDeckY * height));
			await ClickOnPoint(hsHandle, nameDeckPos);
			SendKeys.SendWait(name);
			SendKeys.SendWait("{ENTER}");
		}

		private static double GetXPos(double left, int width, double ratio)
		{
			return (width * ratio * left) + ((width - width * ratio) / 2);
		}

		private static async Task AddCardToDeck(Card card, double ratio, int width, int height, IntPtr hsHandle)
		{
			if(!User32.IsForegroundWindow("Hearthstone"))
			{
				Helper.MainWindow.ShowMessage("Exporting aborted", "Hearthstone window lost focus.");
				return;
			}
			var cardPosX = GetXPos(Config.Instance.ExportCard1X, width, ratio);

			var searchBoxPos = new Point((int)(GetXPos(Config.Instance.ExportSearchBoxX, width, ratio)), (int)(Config.Instance.ExportSearchBoxY * height));

			await ClickOnPoint(hsHandle, searchBoxPos);
			SendKeys.SendWait(FixCardName(card.LocalizedName).ToLowerInvariant());
			SendKeys.SendWait("{ENTER}");

			await Task.Delay(Config.Instance.DeckExportDelay * 2);

			var card2PosX = GetXPos(Config.Instance.ExportCard2X, width, ratio);
			var cardPosY = Config.Instance.ExportCardsY * height;
			for(var i = 0; i < card.Count; i++)
			{
				if(Config.Instance.PrioritizeGolden)
				{
					if(card.Count == 2)
						await ClickOnPoint(hsHandle, new Point((int)card2PosX, (int)cardPosY));
					else if(CheckForGolden(hsHandle, new Point((int)card2PosX, (int)(cardPosY + height * 0.05))))
						await ClickOnPoint(hsHandle, new Point((int)card2PosX, (int)cardPosY));
					else
						await ClickOnPoint(hsHandle, new Point((int)cardPosX, (int)cardPosY));
				}
				else
					await ClickOnPoint(hsHandle, new Point((int)cardPosX, (int)cardPosY));
			}

			if(card.Count == 2)
			{
				//click again to make sure we get 2 cards 
				if(Config.Instance.PrioritizeGolden)
				{
					await ClickOnPoint(hsHandle, new Point((int)cardPosX, (int)cardPosY));
					await ClickOnPoint(hsHandle, new Point((int)cardPosX, (int)cardPosY));
				}
				else
					await ClickOnPoint(hsHandle, new Point((int)card2PosX, (int)cardPosY));
			}

			// Clear search field now all cards have been entered
			await ClickOnPoint(hsHandle, searchBoxPos);
			SendKeys.SendWait("{DELETE}");
			SendKeys.SendWait("{ENTER}");
		}

		private static async Task ClickOnPoint(IntPtr wndHandle, Point clientPoint)
		{
			User32.ClientToScreen(wndHandle, ref clientPoint);

			Cursor.Position = new Point(clientPoint.X, clientPoint.Y);

			//mouse down
			if(SystemInformation.MouseButtonsSwapped)
				User32.mouse_event((uint)User32.MouseEventFlags.RightDown, 0, 0, 0, UIntPtr.Zero);
			else
				User32.mouse_event((uint)User32.MouseEventFlags.LeftDown, 0, 0, 0, UIntPtr.Zero);

			await Task.Delay(Config.Instance.DeckExportDelay);

			//mouse up
			if(SystemInformation.MouseButtonsSwapped)
				User32.mouse_event((uint)User32.MouseEventFlags.RightUp, 0, 0, 0, UIntPtr.Zero);
			else
				User32.mouse_event((uint)User32.MouseEventFlags.LeftUp, 0, 0, 0, UIntPtr.Zero);

			await Task.Delay(Config.Instance.DeckExportDelay);
		}

		private static string FixCardName(string cardName)
		{
			switch(cardName)
			{
					//english
				case "Fireball":
				case "Windfury":
				case "Claw":
					return cardName + " Spell";
				case "Slam":
					return cardName + " Draw";
				case "Silence":
					switch(Config.Instance.SelectedLanguage)
					{
						case "enUS":
							return cardName + " common";
						case "frFR":
							return cardName + " commune";
						default:
							return cardName;
					}

					//german
				case "Feuerball":
				case "Windzorn":
				case "Klaue":
					return cardName + " Zauber";

					//french
				case "Éclair":
					return cardName + " 3";

				default:
					return cardName;
			}
		}

		private static bool CheckForGolden(IntPtr wndHandle, Point point)
		{
			const int width = 50, height = 50, targetHue = 43;
			const float targetSat = 0.38f;
			var avgHue = 0.0f;
			var avgSat = 0.0f;
			var capture = Helper.CaptureHearthstone(point, width, height, wndHandle);

			if(capture == null)
				return false;

			var validPixels = 0;
			for(var i = 0; i < width; i++)
			{
				for(var j = 0; j < height; j++)
				{
					var pixel = capture.GetPixel(i, j);

					//ignore sparkle
					if(pixel.GetSaturation() > 0.05)
					{
						avgHue += pixel.GetHue();
						avgSat += pixel.GetSaturation();
						validPixels++;
					}
				}
			}
			avgHue /= validPixels;
			avgSat /= validPixels;

			return avgHue <= targetHue && avgSat <= targetSat;
		}
	}
}