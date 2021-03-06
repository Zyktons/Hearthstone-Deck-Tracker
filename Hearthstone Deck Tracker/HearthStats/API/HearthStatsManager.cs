﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using MahApps.Metro.Controls.Dialogs;

#endregion

namespace Hearthstone_Deck_Tracker.HearthStats.API
{
	public class HearthStatsManager
	{
		public const int RetryDelay = 5000;
		public const int VersionDelay = 1000;
		private static bool _syncInProgress;

		private static int _backgroundActivities;

		public static bool SyncInProgress
		{
			get { return _syncInProgress; }
		}

		private static void AddBackgroundActivity()
		{
			_backgroundActivities++;
			if(!Helper.MainWindow.ProgressRingTitleBar.IsActive)
				Logger.WriteLine("background process indicator ON", "HearthStatsManager");
			Helper.MainWindow.ProgressRingTitleBar.IsActive = true;
		}

		private static void RemoveBackgroundActivity()
		{
			_backgroundActivities--;
			if(_backgroundActivities <= 0)
			{
				_backgroundActivities = 0;
				if(Helper.MainWindow.ProgressRingTitleBar.IsActive)
					Logger.WriteLine("background process indicator OFF", "HearthStatsManager");
				Helper.MainWindow.ProgressRingTitleBar.IsActive = false;
			}
		}

		public static async Task<PostResult> UploadMatchAsync(GameStats game, Deck deck, bool saveFilesAfter = true, bool background = false)
		{
			Logger.WriteLine("trying to upload match: " + game, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(!HearthStatsAPI.IsValidGame(game))
				return PostResult.Failed;
			if(background)
				AddBackgroundActivity();
			if(!deck.HasHearthStatsId)
			{
				Logger.WriteLine("...deck has no HearthStats id, uploading deck", "HearthStatsManager");
				var success = await UploadDeckAsync(deck);
				if(!success.Success)
				{
					Logger.WriteLine("error: deck could not be uploaded or did not return an id. Can not upload match.", "HearthStatsManager");
					if(background)
						RemoveBackgroundActivity();
					return PostResult.Failed;
				}
			}
			var result = await HearthStatsAPI.PostGameResultAsync(game, deck);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to upload match: " + game, "HearthStatsManager");
				result = await HearthStatsAPI.PostGameResultAsync(game, deck);
			}
			if(result.Success && saveFilesAfter)
				DeckStatsList.Save();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success uploading match " + game, "HearthStatsManager");
			return result;
		}

		public static async Task<PostResult> UploadArenaMatchAsync(GameStats game, Deck deck, bool saveFilesAfter = false,
		                                                           bool background = false)
		{
			Logger.WriteLine("trying to upload arena match: " + game, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();
			if(!deck.HasHearthStatsArenaId)
			{
				Logger.WriteLine("...deck has no HearthStatsArenaId, creating arena run", "HearthStatsManager");
				var createRun = await CreateArenaRunAsync(deck, false, background);
				if(!createRun.Success)
				{
					Logger.WriteLine("error: could not create arena run.", "HearthStatsManager");
					if(background)
						RemoveBackgroundActivity();
					return PostResult.Failed;
				}
			}
			var result = await HearthStatsAPI.PostArenaMatch(game, deck);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to upload arena match: " + game, "HearthStatsManager");
				result = await HearthStatsAPI.PostArenaMatch(game, deck);
			}
			if(result.Success && saveFilesAfter)
				DeckStatsList.Save();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success uploading arena match " + game, "HearthStatsManager");
			return result;
		}

		public static async Task<PostResult> CreateArenaRunAsync(Deck deck, bool saveFilesAfter = false, bool background = false)
		{
			Logger.WriteLine("trying to create arena run for deck " + deck, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();

			var result = await HearthStatsAPI.CreatArenaRunAsync(deck);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to create arena run for deck " + deck, "HearthStatsManager");
				result = await HearthStatsAPI.CreatArenaRunAsync(deck);
			}
			if(result.Success)
			{
				if(saveFilesAfter)
					Helper.MainWindow.WriteDecks();
				if(background)
					RemoveBackgroundActivity();
				Logger.WriteLine("success uploading deck " + deck, "HearthStatsManager");
				return PostResult.WasSuccess;
			}
			if(background)
				RemoveBackgroundActivity();
			return PostResult.Failed;
		}

		public static PostResult UploadMatch(GameStats game, Deck deck, bool saveFilesAfter = true)
		{
			return UploadMatchAsync(game, deck, saveFilesAfter).Result;
		}

		public static async Task<List<Deck>> DownloadDecksAsync(bool forceAll = false)
		{
			Logger.WriteLine("trying to download decks", "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return null;
			}
			var decks = await HearthStatsAPI.GetDecksAsync(forceAll ? 0 : Config.Instance.LastHearthStatsDecksSync);
			if(decks == null || decks.Count == 0)
			{
				Logger.WriteLine("no new decks", "HearthStatsManager");
				return new List<Deck>();
			}
			return decks;
		}

		public static async Task<List<GameStats>> DownloadGamesAsync(bool forceAll = false)
		{
			Logger.WriteLine("trying to download games", "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return null;
			}
			var games = await HearthStatsAPI.GetGamesAsync(forceAll ? 0 : Config.Instance.LastHearthStatsGamesSync);
			if(games == null || games.Count == 0)
			{
				Logger.WriteLine("no new games", "HearthStatsManager");
				return new List<GameStats>();
			}
			return games;
		}

		public static async Task<bool> DeleteDeckAsync(Deck deck, bool saveFilesAfter = true, bool background = false)
		{
			Logger.WriteLine("trying to delete deck " + deck, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return false;
			}
			if(background)
				AddBackgroundActivity();
			var result = await HearthStatsAPI.DeleteDeckAsync(deck);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to delete deck " + deck, "HearthStatsManager");
				result = await HearthStatsAPI.DeleteDeckAsync(deck);
			}
			if(result.Success)
			{
				Logger.WriteLine("success deleting deck " + deck, "HearthStatsManager");
				if(saveFilesAfter)
					Helper.MainWindow.WriteDecks();
				if(background)
					RemoveBackgroundActivity();
			}
			return result.Success;
		}

		public static async Task<PostResult> UploadDeckAsync(Deck deck, bool saveFilesAfter = true, bool background = false)
		{
			//await Task.Delay(1000);
			//return true;
			Logger.WriteLine("trying to upload deck " + deck, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}

			if(background)
				AddBackgroundActivity();
			var first = deck.GetVersion(1, 0);
			var result = await HearthStatsAPI.PostDeckAsync(first);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to upload deck " + deck, "HearthStatsManager");
				result = await HearthStatsAPI.PostDeckAsync(first);
			}
			if(result.Success)
			{
				var versions =
					deck.VersionsIncludingSelf.Where(v => v != new SerializableVersion(1, 0))
					    .Select(deck.GetVersion)
					    .Where(d => d != null && !d.HasHearthStatsDeckVersionId)
					    .ToList();
				if(versions.Any())
				{
					foreach(var v in versions)
					{
						await Task.Delay(VersionDelay);
						await UploadVersionAsync(v, first.HearthStatsIdForUploading, false);
					}
				}
				if(saveFilesAfter)
					Helper.MainWindow.WriteDecks();
				if(background)
					RemoveBackgroundActivity();
				Logger.WriteLine("success uploading deck " + deck, "HearthStatsManager");
				return PostResult.WasSuccess;
			}
			if(background)
				RemoveBackgroundActivity();
			return PostResult.Failed;
		}

		public static PostResult UploadDeck(Deck deck, bool saveFilesAfter = true)
		{
			return UploadDeckAsync(deck, saveFilesAfter).Result;
		}

		public static async void SyncAsync(bool forceFullSync = false, bool background = false)
		{
			Logger.WriteLine(string.Format("starting sync process: forceFullSync={0}, background={1}", forceFullSync, background),
			                 "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return;
			}
			try
			{
				if(_syncInProgress)
				{
					Logger.WriteLine("error: sync already in progress", "HearthStatsManager");
					return;
				}
				_syncInProgress = true;
				if(background)
					AddBackgroundActivity();

				var controller = background
					                 ? null : await Helper.MainWindow.ShowProgressAsync("Syncing...", "Checking HearthStats for new decks...");
				Logger.WriteLine("Checking HearthStats for new decks...", "HearthStatsManager");
				var localDecks = Helper.MainWindow.DeckList.DecksList;
				var remoteDecks = await DownloadDecksAsync(forceFullSync);
				if(remoteDecks.Any())
				{
					var newDecks = remoteDecks.Where(deck => localDecks.All(localDeck => localDeck.HearthStatsId != deck.HearthStatsId)).ToList();
					if(newDecks.Any())
					{
						Helper.MainWindow.FlyoutHearthStatsDownload.IsOpen = true;
						if(!background)
							await controller.CloseAsync();
						newDecks = await Helper.MainWindow.HearthStatsDownloadDecksControl.LoadDecks(newDecks);
						foreach(var deck in newDecks)
						{
							Helper.MainWindow.DeckList.DecksList.Add(deck);
							Helper.MainWindow.DeckPickerList.AddDeck(deck);
							Logger.WriteLine("saved new deck " + deck, "HearthStatsManager");
						}
						Helper.MainWindow.WriteDecks();
						Helper.MainWindow.DeckPickerList.UpdateList();
						background = false;
					}
					if(!background)
					{
						if(controller == null || !controller.IsOpen)
							controller = await Helper.MainWindow.ShowProgressAsync("Syncing...", "Checking for new versions...");
						else
							controller.SetMessage("Checking for new versions...");
					}
					Logger.WriteLine("Checking for new versions...", "HearthStatsManager");
					var decksWithNewVersions =
						remoteDecks.Where(
						                  deck =>
						                  localDecks.Any(
						                                 localDeck =>
						                                 localDeck.HasHearthStatsId && deck.HearthStatsId == localDeck.HearthStatsId
						                                 && localDeck.GetMaxVerion() != deck.GetMaxVerion())).ToList();
					if(decksWithNewVersions.Any()) // TODO: TEST
					{
						foreach(var deck in decksWithNewVersions)
						{
							var currentDeck = localDecks.FirstOrDefault(d => d.HasHearthStatsId && d.HearthStatsId == deck.HearthStatsId);
							if(currentDeck == null)
								continue;
							var originalDeck = (Deck)currentDeck.Clone();
							var versions =
								deck.VersionsIncludingSelf.Where(v => !currentDeck.VersionsIncludingSelf.Contains(v))
								    .OrderBy(v => v)
								    .Select(v => deck.GetVersion(v))
								    .Where(v => v != null)
								    .ToList();
							if(versions.Any())
							{
								foreach(var newVersion in versions)
								{
									var clone = (Deck)currentDeck.Clone();
									currentDeck.Version = newVersion.Version;
									currentDeck.SelectedVersion = newVersion.Version;
									currentDeck.HearthStatsDeckVersionId = newVersion.HearthStatsDeckVersionId;
									currentDeck.Versions.Add(clone);
								}
								Helper.MainWindow.DeckPickerList.RemoveDeck(originalDeck);
								Helper.MainWindow.DeckPickerList.AddDeck(currentDeck);
								Logger.WriteLine(
								                 string.Format("saved {0} new versions ({1}) to {2}", versions.Count,
								                               versions.Select(v => v.Version.ToString()).Aggregate((c, n) => c + ", " + n), deck),
								                 "HearthStatsManager");
							}
						}
						Helper.MainWindow.WriteDecks();
						Helper.MainWindow.DeckPickerList.UpdateList();
					}
					if(!background)
					{
						if(controller == null || !controller.IsOpen)
							controller = await Helper.MainWindow.ShowProgressAsync("Syncing...", "Checking for edited decks...");
						else
							controller.SetMessage("Checking for edited decks...");
					}
					Logger.WriteLine("Checking for edited decks...", "HearthStatsManager");
					var editedDecks =
						remoteDecks.Where(
						                  r =>
						                  !localDecks.Any(
						                                  l =>
						                                  l.HasHearthStatsId && l.HearthStatsId == r.HearthStatsId
						                                  && (l.Name != r.Name || l.Tags != r.Tags || l.Note != r.Note
						                                      || l.Cards.Any(lc => r.Cards.All(rc => !rc.EqualsWithCount(lc)))))).ToList();
					if(editedDecks.Any())
					{
						foreach(var deck in editedDecks)
						{
							var localDeck = localDecks.FirstOrDefault(d => d.HasHearthStatsId && d.HearthStatsId == deck.HearthStatsId);
							if(localDeck != null)
							{
								localDeck = (Deck)localDeck.Clone();
								localDeck.Name = deck.Name;
								localDeck.Tags = deck.Tags;
								localDeck.Note = deck.Note;
								localDeck.Cards.Clear();
								foreach(var card in deck.Cards)
									localDeck.Cards.Add((Card)card.Clone());
								Logger.WriteLine("edited latest version of " + localDeck, "HearthStatsManager");
							}
						}
						Helper.MainWindow.DeckPickerList.UpdateList();
						Helper.MainWindow.WriteDecks();
						Logger.WriteLine("edited " + editedDecks.Count + " decks", "HearthStatsManager");
					}
				}

				if(!background)
				{
					if(controller == null || !controller.IsOpen)
						controller = await Helper.MainWindow.ShowProgressAsync("Syncing...", "Checking HearthStats for new matches...");
					else
						controller.SetMessage("Checking HearthStats for new matches...");
				}
				Logger.WriteLine("Checking HearthStats for new matches...", "HearthStatsManager");
				var newGames = await DownloadGamesAsync(forceFullSync);
				if(newGames.Any())
				{
					foreach(var game in newGames)
					{
						var deck =
							Helper.MainWindow.DeckList.DecksList.FirstOrDefault(
							                                                    d =>
							                                                    d.VersionsIncludingSelf.Select(v => d.GetVersion(v.Major, v.Minor))
							                                                     .Where(v => v != null)
							                                                     .Any(v => game.BelongsToDeckVerion(v)));
						if(deck == null)
						{
							Logger.WriteLine(string.Format("no deck found for match {0}", game), "HearthStatsManager");
							continue;
						}
						if(deck.DeckStats.Games.Any(g => g.HearthStatsId == game.HearthStatsId))
						{
							Logger.WriteLine(string.Format("deck {0} already has match {1}", deck, game), "HearthStatsManager");
							continue;
						}
						Logger.WriteLine(string.Format("added match {0} to version {1} of deck {2}", game, deck.Version.ShortVersionString, deck),
						                 "HearthStatsManager");
						deck.DeckStats.AddGameResult(game);
					}
					DeckStatsList.Save();
					Helper.MainWindow.DeckPickerList.UpdateList();
					Helper.MainWindow.DeckStatsFlyout.LoadOverallStats();
				}

				if(!background)
					controller.SetMessage("Checking for new local decks...");
				Logger.WriteLine("Checking for new local decks...", "HearthStatsManager");
				var newLocalDecks = localDecks.Where(deck => !deck.HasHearthStatsId && deck.IsArenaDeck != true).ToList();
				if(newLocalDecks.Any(d => d.SyncWithHearthStats != false))
				{
					Logger.WriteLine("found " + newLocalDecks.Count + " new decks", "HearthStatsManager");
					if(!background)
						await controller.CloseAsync();
					Helper.MainWindow.FlyoutHearthStatsUpload.IsOpen = true;
					newLocalDecks = await Helper.MainWindow.HearthStatsUploadDecksControl.LoadDecks(newLocalDecks);
					if(newLocalDecks.Any())
					{
						controller = await Helper.MainWindow.ShowProgressAsync("Syncing...", "Uploading " + newLocalDecks.Count + " new decks...");
						Logger.WriteLine("Uploading " + newLocalDecks.Count + " new decks...", "HearthStatsManager");
						await Task.Run(() => { Parallel.ForEach(newLocalDecks, deck => UploadDeck(deck, false)); });
						Helper.MainWindow.WriteDecks(); //save new ids
						background = false;
					}
				}

				if(!background)
				{
					if(controller == null || !controller.IsOpen)
						controller = await Helper.MainWindow.ShowProgressAsync("Syncing...", "Checking for new local versions...");
					else
						controller.SetMessage("Checking for new local versions...");
				}

				Logger.WriteLine("Checking for new local versions...", "HearthStatsManager");
				var localNewVersions =
					localDecks.Where(x => x.HasHearthStatsId)
					          .SelectMany(
					                      x =>
					                      x.Versions.Where(v => !v.HasHearthStatsDeckVersionId)
					                       .Select(v => new {version = v, hearthStatsId = x.HearthStatsId}))
					          .ToList();
				if(localNewVersions.Any())
				{
					if(!background)
						controller.SetMessage("Uploading " + localNewVersions.Count + " new versions...");
					Logger.WriteLine("Uploading " + localNewVersions.Count + " new versions...", "HearthStatsManager");
					//this can't happen in parallel (?)
					foreach(var v in localNewVersions)
					{
						var result = await UploadVersionAsync(v.version, v.hearthStatsId, false);
						if(!result.Success && result.Retry)
						{
							await Task.Delay(RetryDelay);
							await UploadVersionAsync(v.version, v.hearthStatsId, false);
						}
					}
					Helper.MainWindow.WriteDecks();
				}
				if(!background)
					controller.SetMessage("Checking for new local matches...");
				Logger.WriteLine("Checking for new local matches...", "HearthStatsManager");

				var newMatches =
					Helper.MainWindow.DeckList.DecksList.Where(d => d.SyncWithHearthStats == true && d.HasHearthStatsId)
					      .SelectMany(d => d.DeckStats.Games.Where(g => !g.HasHearthStatsId).Select(g => new {deck = d, game = g}))
					      .ToList();
				if(newMatches.Any())
				{
					if(!background)
						controller.SetMessage("Uploading " + newMatches.Count + " new matches...");
					Logger.WriteLine("Uploading " + newMatches.Count + " new matches...", "HearthStatsManager");
					await Task.Run(() => { Parallel.ForEach(newMatches, match => UploadMatch(match.game, match.deck, false)); });
					DeckStatsList.Save();
				}
				Config.Instance.LastHearthStatsDecksSync = DateTime.Now.ToUnixTime() - 600; //10 minute overlap
				Config.Instance.LastHearthStatsGamesSync = DateTime.Now.ToUnixTime() - 600;
				Config.Save();
				if(!background)
					await controller.CloseAsync();

				RemoveBackgroundActivity();
				_syncInProgress = false;
				Logger.WriteLine("finished sync process", "HearthStatsManager");
			}
			catch(Exception e)
			{
				Logger.WriteLine("There was an error syncing with HearthStats:\n" + e, "HearthStatsManager");
				_syncInProgress = false;
			}
		}

		public static async Task<PostResult> UploadVersionAsync(Deck deck, string hearthStatsId, bool saveFilesAfter = true,
		                                                        bool background = false)
		{
			Logger.WriteLine("trying to upload version " + deck.Version + " of " + deck, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();

			var result = await HearthStatsAPI.PostVersionAsync(deck, hearthStatsId);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to upload version " + deck.Version + " of " + deck, "HearthStatsManager");
				result = await HearthStatsAPI.PostVersionAsync(deck, hearthStatsId);
			}
			if(result.Success && saveFilesAfter)
				Helper.MainWindow.WriteDecks();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success uploading version " + deck, "HearthStatsManager");
			return result;
		}

		public static PostResult UploadVersion(Deck deck, string hearthStatsId, bool saveFilesAfter = true)
		{
			return UploadVersionAsync(deck, hearthStatsId, saveFilesAfter).Result;
		}

		public static async Task<PostResult> DeleteMatchesAsync(List<GameStats> games, bool saveFilesAfter = true, bool background = false)
		{
			Logger.WriteLine("trying to delete game " + games.Select(g => g.ToString()).Aggregate((c, n) => c + ", " + n), "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();
			var result = await HearthStatsAPI.DeleteMatchesAsync(games);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to delete game " + games, "HearthStatsManager");
				result = await HearthStatsAPI.DeleteMatchesAsync(games);
			}
			if(result.Success && saveFilesAfter)
				DeckStatsList.Save();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success deleting game " + games, "HearthStatsManager");
			return result;
		}

		public static async Task<PostResult> MoveMatchAsync(GameStats game, Deck target, bool saveFilesAfter = true, bool background = false)
		{
			Logger.WriteLine("trying to move game " + game + " to " + target, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();
			var result = await HearthStatsAPI.MoveMatchAsync(game, target);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to move game " + game + " to " + target, "HearthStatsManager");
				result = await HearthStatsAPI.MoveMatchAsync(game, target);
			}
			if(result.Success && saveFilesAfter)
				DeckStatsList.Save();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success moveing game " + game, "HearthStatsManager");
			return result;
		}

		public static async Task<PostResult> UpdateDeckAsync(Deck deck, bool saveFilesAfter = true, bool background = false)
		{
			Logger.WriteLine("trying to update deck " + deck, "HearthStatsManager");
			if(!HearthStatsAPI.IsLoggedIn)
			{
				Logger.WriteLine("error: not logged in", "HearthStatsManager");
				return PostResult.Failed;
			}
			if(background)
				AddBackgroundActivity();
			var result = await HearthStatsAPI.UpdateDeckAsync(deck);
			if(!result.Success && result.Retry)
			{
				await Task.Delay(RetryDelay);
				Logger.WriteLine("try #2 to update deck " + deck, "HearthStatsManager");
				result = await HearthStatsAPI.UpdateDeckAsync(deck);
			}
			if(result.Success && saveFilesAfter)
				Helper.MainWindow.WriteDecks();
			if(background)
				RemoveBackgroundActivity();
			if(result.Success)
				Logger.WriteLine("success updating deck " + deck, "HearthStatsManager");
			return result;
		}
	}
}