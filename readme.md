# Localization From Source

This library aims to simplify the process of localizing Stardew Valley mods in two ways:

* Automating the detection of out-of-date translations and reporting them clearly
* Allowing the source of truth for strings to remain in the C# code.

That first bullet point means that with this package, you go from having a `de.json`
that looks like:

```json
{
    // House events
    "help-with-monster": "Helfen! Helfen!  Da ist ein Monster im Keller!",
```

to this:

```json
// This translation is incomplete!  If you're able to help, search for ">>>" in this file
// and address the issues described.  If you can do that, please send the corrected file back
// to the author at github:NermNermNerm or nexusmods:NermNermNerm.
//
// Built commit: 9fef11ee437e5affe73070bbe029a33205990067
{
    // https://github.com/NermNermNerm/Junimatic/blob/8fd9205876fed30b2c0f9d0ee7e1871b96c51b3c/Junimatic/i18n/default.json#L8
    // >>>SOURCE STRING CHANGED - originally translated by nexus:playerone on 5/6/2021
    // old: "Help! Help!  There is a monster in the basement!"
    // new: "Help! Help!  There is a monster in the attic!"
    "help-with-monster": "Helfen! Helfen!  Da ist ein Monster im Keller!",
```

For the translator, you can see that we've given them context on exactly what the problem
with the translation is and a pointer to the file on github so they can do further
research into what else changed when the monster moved into the attic.  For the
mod owner, they no longer have to guess at what version of the translator was working
with to give them confidence that the localization was actually done correctly.
That confidence is underscored by build-time analysis that alerts us if, say the
translator missed a new translation.


The next feature of this package is mainly of interest to mods that haven't been translated
yet or expect to add a substantial amount of new code.  Instead of localizing by taking
something like this:

```C#
Game1.addHUDMessage(new HUDMessage("The sun seems a little brighter today."));
```

And then cut & paste the string into the default.json, and change the C# into:

```C#
Game1.addHUDMessage(new HUDMessage(I18n.HudMessage_SunBrighter));
```

Instead of fussing with cut&paste at all, you'd just change it like this:

```C#
Game1.addHUDMessage(new HUDMessage(L("The sun seems a little brighter today.")));
```

Not only does that make life easier for you, when converting your mod, it also makes it easier
for localizers because the github links generated by the previously-described system will
be able to link directly into the code, so instead of you having to try and come up with clever
keys that will maybe give your translators a clue as to how the translation is used, they can
link back to the code and see all the context.  E.g. the key we chose for this example,
says "HudMessage_SunBrighter" - which certainly told the translator that this was a HUD
message and not, say, a dialog, but we didn't give any clue as to what might have caused
the sun to be brighter.  When it comes to doing accurate localization, nuance is often
everything.  (E.g. is the Sun *actually* brighter?  Or is this just an expression to convey
a sense of optimism?  If it's an expression, does a literal translation convey the same meaning
in *all* cultures?)  You could try to encompass that fact in your choice
of key, but it's extremely difficult to anticipate everything that might be different
across all cultures.

## Getting Started




## Crowd-Sourced Localization

Games and other production software are localized by professionals.  These professionals cost money, as they
need to understand the source language, the target language in all its nuances, and the product they're
localizing.  Usually they're given the strings to be localized in a big pile, a few weeks before the
release and then again as patches come out, because the patches need to be localized in all markets
simultaneously, so as not to give the impression that one market is more important than another.

In mods and a fair number of industrial settings, this doesn't really work, because there's just no
money to pay those highly skilled localizers.  The industrial answer (just don't localize
what you can't afford to localize), won't cut it for game mods, and as it's a mod anyway, it
ought to be flexible enough to allow for users to localize it.  And what do you know?  The rare
skill-set that we were paying all that money for - knowing the local language, knowing the source
language, and knowing the application is a skill-set that many mod users will have!

Hence mods tend to follow the same model that shipped products follow, except that the translations
aren't compiled into the product so they can be tweaked locally.  Further, mods tend to use the same
idea that's prevailed in the world of localization since localization became a serious thing back
in the 80's and 90's.

But mod authors are still, even with that flexibility, playing by the same rules as the professional
coders do - that is they dump a huge set of changes out for some multilingual enthusiast to pick up and
hope for the best.  That works great for a while, but as the code changes, the translations follow only
in fits and starts.  When, say, 90% of the strings are converted correctly, it's hard for a new enthusiast
to get involved because they don't know:

1. Is this string awkward because the translation is wrong?
2. Is it because the mod is broken?
3. Is it because the mod has changed and the translation hasn't kept up?
4. Is it because I don't understand how the mod is supposed to work?

...And in any case the speech dialog just closed so I don't even know what I saw.

If they dug into it, they could find something like this in the de.json file, but they'd have to read
the whole file themselves to find this line:

```json
    "help-with-monster": "Helfen! Helfen!  Da ist ein Monster im Keller!",
```

If they dug further, they could open up the default.json, then find the corresponding source string
and possibly figure it out that the translation is bad.  Maybe.

### A better experience

This package aims to provide a better experience.  The moment when a player encounters a missing or
outdated translation, there is a log message and (optionally) a chat dialog that says "Hey, this
mod's translation is incomplete in your language!  Here's the file that needs fixed: ...\Mods\somemod\i18n\de.json"
Even a first-timer could easily find the file without trouble and they'd feel perhaps a bit more comfortable
doing it.  When they first open the file, there's a comment at the top line gives them further direction:

```json
// This translation is incomplete!  If you're able to help, search for ">>>" in this file
// and address the issues described.  If you can do that, please send the corrected file back
// to the author at github:NermNermNerm or nexusmods:NermNermNerm.
//
// Built commit: 9fef11ee437e5affe73070bbe029a33205990067
{
```

If they do search, then instead of just a key and a value, they get this:

```json
    // https://github.com/NermNermNerm/Junimatic/blob/8fd9205876fed30b2c0f9d0ee7e1871b96c51b3c/Junimatic/i18n/default.json#L8
    // >>>SOURCE STRING CHANGED - originally translated by nexus:playerone on 5/6/2021
    // old: "Help! Help!  There is a monster in the basement!"
    // new: "Help! Help!  There is a monster in the attic!"
    "help-with-monster": "Helfen! Helfen!  Da ist ein Monster im Keller!",
```

This case is pretty clear-cut how the translation needs to be changed, but if it's more complicated, there's a link back
to the source on GitHub so (given a certain amount of expertise), a person could use blame-analysis to see what else changed
in the commit that moved the monster into the attic.

### How things change for mod authors

Putting together a fully-commented `de.json` like that requires either a great deal of meticulous work by the mod author
or some extra tooling.  This package provides that tooling.  The way it works is that this package writes the translated
json files from data it maintains.

When you first create a mod, your i18n folder just contains the one file, `default.json`.  When that first player
decides to help you out with a translation, they'll do just like they've always done - they'll copy the `default.json`
and change all the English strings to their language and send you the file, say `de.json`.  Instead of just pasting
that file directly, in this new world you'd run:

```powershell
cd DIRECTORY_WITH_CSPROJ
git branch ingestde 9fef11ee437e5affe73070bbe029a33205990067
git checkout ingestde
msbuild /t:IngestTranslation /p:TranslatedFile=PATH/TO/SUPPLIED/de.json;TranslationAuthor=playertwo
git add --all .
git merge main
git commit
```

The idea of all the `git` shenanigans is to ensure that we start from the commit that built the `default.json` that
our new friend *playertwo* translated.  Again, you can find it in the header commit for the translated json file that you
received.  All that branching stuff is unnecessary if the default.json hasn't changed since you last released and
the translation is coming from the latest release, which will commonly be the case.

When all the dust settles, a new file will be created, `i18nSource\de.json`:

```json
// Do not manually edit this file!
// Instead, collect updates to the translation files distributed with your package and
// use the tooling to merge the changes like this:
//
// msbuild /target:IngestTranslations /p:TranslatedFile=<path-to-file>.json;TranslationAuthor=<author-id>
//
// Where 'author-id' is '<platform>:<moniker>' where '<platform>' is something like 'nexus' or 'github' and
// '<id>' is the identity of the person who supplied the translations on that platform.
{
  "translations": [
    {
      "source": "Help! Help!  There is a monster in the basement!",
      "translation": "Helfen! Helfen!  Da ist ein Monster im Keller!",
      "author": "nexus:playerone",
      "ingestionDate": "2021-05-6T11:41:40.6409893-07:00"
    },
```

Then, when you build, you get the annotated `i18n\de.json`.

You'll note that in that translation entry there was no key in the data at all.  That might seem jarring,
but remember - the translator probably didn't look at the key very hard either.  They translated the string,
not the key.  The key is meant to describe the scenario, and that description can't really be made better
if changing it entails changing a dozen translation files that you probably can't test.  Hence the compiler
step that generates `i18n\de.json` compares the strings in `default.json` with the strings in `i18nSource\de.json`
and only then generates the key-to-translation mapping.



## What it does

The traditional way to localize a Stardew Valley mod involves finding every localizable string (by eye), copying them to
the `default.json` file (while being careful to escape for json), inventing a key, and changing the code to use that
key...  and then retesting the entire mod carefully because you could have goofed up any of those manual steps.
With this system, you just mark all your localizable strings with an `L`, like so:

```C#
ModEntry.AddQuestItem(
    objects,
    OldJunimoPortalQiid,
    L("a strange little structure"),
    L("At first it looked like a woody weed, but a closer look makes it like a little structure, and it smells sorta like the Wizard's forest-magic potion."),
    0);
```

A compile-time step takes care of populating the default.json file.  Note that the `L` isn't magic, it works
only if you have this line in your using block:

```C#
using static LocalizeFromSourceLib.LocalizeMethods;
```

### How it helps Translators

Now let's talk about what happens after someone creates a localized version of your default.json.  It's certainly easy
to do on the first go-around.  They just copy/paste your default.json into `de.json` and convert all the English strings
within it to German.  Where problems start is when you update a string or add a new one.  Then you have to get your
German translator back on-line and have them diff your default.json file (which may not be easy for a non-technical
person to do), and produce the necessary changes.

This package automates that by maintaining the `de.json` file for you (doing things like deleting keys out of there when
you delete strings from the code) and it also produces a `de.edits.json` file that spells out exactly what changed
for the translators.  For each change, it shows the old source-language string, a github link to the code where the
string was found, the string's new value, and the old translation.  It leaves a spot for the translator to fill in
the new value so all they have to do is edit that one file.  When you rebuild with the updated `de.edits.json` file,
it will move those strings into the `de.json` file and clean up the `de.edits.json` file once everything lines up.

This doesn't just aim at making it easier for the localizer to keep translations current - it aims at making it more
likely that they will do it.  As it stands now, what tends to happen is that a multilingual player sees your mod,
likes it, enthuses over it, asks to do a translation so that their friends can play it too, then disappears entirely.
New multi-lingual players appear all the time, but it's pretty daunting to chime in and offer to update the
translations - what state was it left in?  When was it last translated?  What strings are actually broken?
Will the previous translator ever reappear?  All these questions and more make it hard for people to step in
and fix things.  If there's a single file they can look at on GitHub that tells them exactly what needs to be updated,
they can just update that file and mail you the update and leave it up to you to apply it.

### Mixed code and localizable strings

In Stardew Valley, there are cases where what amounts to game code is mixed into a localizable string.

```C#
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(editor =>
                {
                    IDictionary<string, string> data = editor.AsDictionary<string, string>().Data;
                    data[MeetLinusAtTentQuest] = "Basic/Find Linus At His Tent/Linus said he had something he needed your help with./Go to Linus' tent before 10pm/null/-1/0/-1/false";
                    data[MeetLinusAt60Quest] = "Basic/Meet Linus At Level 60/Linus had something he wanted to show you at level 60 of the mines./Follow Linus to level 60/null/-1/0/-1/false";
                    data[CatchIcePipsQuest] = "Basic/Catch Six Ice Pips/Catch six ice pips and put them in the mysterious fish tank.//null/-1/0/-1/false";
                });
            }
```

With this system, you *could* just wrap those strings in `L()` and call it a day.  At that point you'd be pretty
much at the same point as if you used the traditional localization system.  With this package, you can move it one step farther and wrap it in `SdvQuest`, like so:

```C#
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(editor =>
                {
                    IDictionary<string, string> data = editor.AsDictionary<string, string>().Data;
                    data[MeetLinusAtTentQuest] = SdvQuest("Basic/Find Linus At His Tent/Linus said he had something he needed your help with./Go to Linus' tent before 10pm/null/-1/0/-1/false");
                    data[MeetLinusAt60Quest] = SdvQuest("Basic/Meet Linus At Level 60/Linus had something he wanted to show you at level 60 of the mines./Follow Linus to level 60/null/-1/0/-1/false");
                    data[CatchIcePipsQuest] = SdvQuest("Basic/Catch Six Ice Pips/Catch six ice pips and put them in the mysterious fish tank.//null/-1/0/-1/false");
                });
            }
```

That tells the system that the string is a quest, and it will actually add just the localizable parts to `default.json`.
That is, in this case, it wouldn't add `"key": "Basic/Find Linus...`, it will add `"key1": "Find Linus At His Tent"` and
`"key2": "Linus said...` and so on.  The win here is that if you ever have to update something in the non-localizable
part of the string, you can do so *without* having to make the same change across all your languages.

For quests, that's nice.  But there's also a `SdvEvent` method, and, well, I think the power of that is self-evident.

### Ensuring everything that should be localized is localized

Generally, you can eyeball when a string should be localized and you can do a pretty good job of finding them.
But if "pretty good" isn't what you're after and you want static analysis to help ensure that you've got everything,
this package can do that.  Turn on `strict` mode by adding a `LocalizeFromSource.json` file to your project:

```C#
{
    "isStrict": true,
    "invariantStringPatterns": [
        // dot and slash-separated identifiers
        "^\\w+[\\./\\\\_][\\w\\./\\\\_]*\\w$",

        // CamelCase or pascalCase identifiers - identified by all letters and digits with a lowercase letter right before an uppercase one.
        "^\\w*[\\p{Ll}\\d][\\p{Lu}]\\w*$",

        // Qualified item id's (O)blah.blah or (BC)Chest
        "^\\([A-Z]+\\)[\\p{L}\\d\\.]+$"
    ],
    "invariantMethods": [
        "StardewValley.Farmer.getFriendshipHeartLevelForNPC",
        "StardewValley.Game1.playSound"
    ]
}
```

The other properties assist in weeding out false-positives.  `invariantStringPatterns` allows you to describe strings that can be mechanically identified as non-localizable.  `invariantMethods` allow you to list methods that take
string identifiers as arguments.

> Note: the patterns shown in this example are actually already in the default list of invariant patterns for Stardew Valley.
> Similarly for the method list.  They're just listed in this example to give you a clearer idea of what to put here.

There will always be cases that you can't mechanically identify or it's just not
worth the hassle of editing the json, for those cases, you can use `I()`, similar to `L`:

```C#
var c = farmAnimals.Values.Count(a => a.type.Value.EndsWith(I(" Chicken")));
```

### Format strings

The perils of `String.Format(identifier, x, y)` have been known for a long time, and a number of remedies have
been devised with varying degrees of effectiveness.  Interpolated strings is certainly one of the most powerful
of them, and this package aims to exploit them.  Alas, it can't quite be done seamlessly, you have to use `LF`
instead of just `L`.  Like so:

```C#
quest.currentObjective = LF($"{count} of 6 teleported");
```

If you do that, then you'll see a string like this generated in your default.json `"{{arg0}} of 6 teleported"`.

Note that there is also a version for invariant strings, `IF`.  Why can't we just do `L`?  It's a long story
involving how the compiler works.  If left to its own devices, $"{count} of 6 teleported" will get turned
into instructions that look a lot like allocating a `StringBuilder` and appending the count and the string to it.
If, on the other hand, you pass an interpolated string to a method that takes a `System.FormattableString` as
an argument, then it constructs such an object and passes that to the method.  That's the behavior that
`LF` is counting on.  "Aha!"  I hear you say.  "Just have an overload of `L` that takes a string and another
that takes `FormattableString`!  Problem solved!"  Ah, would that it were true.  If you do that, you'll find
that the `FormattableString` overload never gets called.  That's because the type of the object that
the compiler generates is not actually `FormattableString` but instead is a subclass, which is internal,
making it so there isn't an exact type-match with either overload.  The compiler has two options to convert
to a type that will match one of the overloads - there's the subclass conversion and, of course, the
interpolated string has a string conversion.  It picks the string conversion because conversions to base
types are always preferred.  If you know a way to beat that, please raise an Issue!  It'd sure be nice if it
could be overloaded!

Note that a .net `FormattableString` uses the String.Format style of formatting, e.g. "{0}" and it shows
up in the `i18n\*.json` files as "{{arg0}}".  That's because this package translates from the .net style
to the SDV style.  The main reason why it does that is simply to make it familiar to translators who have
done SDV translations in the past and, in the event there's any tooling out there, make it compatible with
that too.

The idea of using "{{name}}" rather than "{0}" in SDV has a couple of purposes, one being to give translators
a little bit of context on what placeholders mean.  For open source mods using this package, you probably
don't need to do that as reviewers now who need more context can use the link and look at the source code,
which will provide far more detail on what a placeholder might be replaced with than any single identifier
can provide.  However, if you really feel like it's important, you can pick your own names (rather than
'arg0', 'arg1' and so on) by writing your string like this: $"{count}|count| of 6 teleported".  Basically
you just put the name you want to use (an identifier) surrounded by pipe characters immediately after the
format.  That will make it show up as "{{count}} of 6 teleported" in the default.json.

## Installing and using the package

1. Install the 'NermNermNerm.Stardew.LocalizeFromSource' NuGet package.
2. Edit the `<PropertyGroup>` block of your `.csproj` file to add these lines:

```xml
<BundleExtraAssemblies>ThirdParty</BundleExtraAssemblies>
<IgnoreModFilePatterns>\.edits\.json$,new-language-template.json$</IgnoreModFilePatterns>
```

> Note that `BundleExtraAssemblies` might bring in other DLL's into your package than you intend.  After your
> compile with this setting, look at the files that were copied into the Mod folder and make sure that only
> the LocalizeFromSource.dll was added.  If there are unwanted DLL's in there, add them to `IgnoreModFilePatterns`.
 
3. In your ModEntry, add these lines to hook up the translator:

```C#
using static NermNermNerm.Stardew.LocalizeFromSource.SdvLocalize;
.
.
.
    public override void Entry(IModHelper helper)
    {
        // Lets the library know what the locale is
        Initialize(() => helper.Translation.Locale);

        // Lets the library report errors
        OnBadTranslation += (message) => this.Monitor.LogOnce(IF($"Translation error: {message}"), LogLevel.Info);
        OnTranslationFilesCorrupt += (message) => this.Monitor.LogOnce(IF($"Translation error: {message}"), LogLevel.Error);

        // Optional - changes localized strings to have umlauts and accent marks to make it easier to see
        //  localized strings when running the game in your native language.
#if DEBUG
        DoPseudoLoc = true;
#endif
```

4. This step is not actually particular to using this library for localization.  SDV changes the Locale to the one
   selected by the user only after some assets are already loaded.  To my knowledge, this includes objects and buildings
   and crafting recipes (but recipes don't contain any localized text so they don't matter).  If you have any custom
   objects or buildings, add a line like this:

```C#
this.Helper.Events.Content.LocaleChanged += (_,_) => this.Helper.GameContent.InvalidateCache("Data/Objects");
```

5. In each C# file that contains the strings that should be translated add this to the using blocks:

```C#
using static NermNermNerm.Stardew.LocalizeFromSource.SdvLocalize;
```

6. For each translatable string, wrap them in `L` if they are plain strings, `LF` if they are format strings.
   (And, if you are using String.Format, convert them to interpolated strings, like `$"x is {x}"`).

If you do all these things and compile, you should see that a `default.json` file was generated.  Indeed, if somebody
supplies you with a language file (like a `de.json` file) that translates all the values in `default.json`, it should
just work if you switch languages.

## Common pitfalls

The argument to `L` **MUST** be a constant string and likewise `LF` must take a constant format string.

```C#
string s = "world";
spew(L("hello")); // Okay
spew(L($"hello {s}")); // Not okay
spew(LF($"hello {s}")); // Okay, but if s is a string, ensure it's localized appropriately!

spew(L(condition ? "hello" : "world")); // Not okay.
spew(condition ? L("hello") : L("world")); // Okay
```

Note that this package has both compile-time and run-time elements.  All the faults above generate compile-time errors.

## Quests and Events

There are special versions of `L` for use with quest and event descriptors.  The event one is straightforward:

```C#
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(editor =>
                {
                    IDictionary<string, string> data = editor.AsDictionary<string, string>().Data;
                    data[MeetLinusAtTentQuest] = SdvQuest("Basic/Find Linus At His Tent/Linus said he had something he needed your help with./Go to Linus' tent before 10pm/null/-1/0/-1/false");
                    data[MeetLinusAt60Quest] = SdvQuest("Basic/Meet Linus At Level 60/Linus had something he wanted to show you at level 60 of the mines./Follow Linus to level 60/null/-1/0/-1/false");
                    data[CatchIcePipsQuest] = SdvQuest("Basic/Catch Six Ice Pips/Catch six ice pips and put them in the mysterious fish tank.//null/-1/0/-1/false");
                });
            }
```

It's just a matter of using `SdvQuest` instead of `L`.  The advantage of this is that it will produce localizable strings for each part of the quest in the `default.json` instead of the whole thing, so if ever you need to tweak the non-localizable parts, you can do that without having to mess with the translations.

Events can be that simple too, depending on where you are storing your event code.  The approach that this system
supports is for if you are pasting your event code directly into your C#.

```C#
        private void EditFarmHouseEvents(IDictionary<string, string> eventData)
        {
            eventData[IF($"{MiningJunimoDreamEvent}/H/sawEvent {ReturnJunimoOrbEvent}/time 600 620")]
                = SdvEvent($@"grandpas_theme/
-2000 -1000/
farmer 13 23 2/
skippable/
fade/
addTemporaryActor Grandpa 1 1 -100 -100 2 true/
specificTemporarySprite grandpaSpirit/
viewport -1000 -1000 true/
pause 8000/
speak Grandpa ""My dear boy...^My beloved grand-daughter...#$b#I am sorry to come to you like this, but I had to thank you for rescuing my dear Junimo friend.#$b#He protected me at a time when my darkest enemy was my own failing mind.#$b#In better days, he helped me with my smelters and other mine-related machines.  He will help you too; he really enjoys watching the glow of the fires!#$b#I rest much easier now knowing that my friend is safe.  I am so proud of you...""/playmusic none/
pause 1000/
end bed");
        }
```

You see that there's a `SdvEvent` wrapping the event descriptor.  This method insists that the value be a formatted
string simply because you *might* be injecting some code in there.  (For example, maybe you have a custom event command
and have a constant for the name of your command.)

Like with quests, `SdvEvent` parses the event code and looks for localizable things within that code and puts just that
into the `default.json`.  The implementation as of now is pretty dumb.  It just looks for things between quotes, filters
out things that look like identifiers and/or paths, and puts them into default.json.  It's true that there are a finite
number of *stock* commands that require localized text (e.g. the `speak` in this example), but there are custom event
commands that we can't know about.  The upshot is to take a careful look at your event code and ensure that nothing is
quoted that doesn't need to be and that all dialog is quoted.  You can also look at your generated `default.json` file
to see if anything incorrect showed up there.  If it does, it's not the end of the world, just make sure your translators
know to leave that string alone.  And, of course, raise an Issue about it so that better solutions can be imagined.

## Enabling 'Strict' mode

First off let's be clear here:  This package is using heuristics and user-supplied clues to sort out what needs to be
localized and what doesn't.  It's not foolproof, and turning this mode on will open you up to some daily friction in
your coding.  This package has a lot of tools designed to reduce that friction, but it'll never zero it out.  Is it
worth it?  That's up to you.  Also, the tools we're describing here have the potential to overdo it - meaning that
they may confuse a localizable string for an invariant one, causing mistakes.  Again, it's up to you.  Use these
tools carefully.

With those disclaimers out of the way, let's get on with it.  The first thing you should do is to add `L` and `LF`
strings just by inspection.  The goal of doing this first is to, as much as possible, make your first build in
strict mode just show the false-positives.  Once that's done and compiling well, create a file called
`LocalizeFromSource.json` in the same folder as your .csproj file:

```C#
{
    "isStrict": true,
    "invariantStringPatterns": [],
    "invariantMethods": []
}
```

The first build after that will display all the findings.  Note that the errors spewed in this way are using
a relatively primitive means of reporting errors in Visual Studio, and so the line numbers will not keep up with
changes in the source code, so beware of that.  In any case, use this first build to just get an overview of what
you need to change.  You'll probably be able to assemble them into these categories:

* String identifiers that are passed to methods.  Strict mode, by default, has some ways of identifying string
  identifiers (e.g. looking for `camelCase` or `path/characters`...) but there are cases like `Abigail` (where
  there's just one word) that aren't clear-cut enough.  For these cases, particularly api's that you use several
  times in your app, consider adding a line to the `invariantMethods` list with the fully-qualified-name of the
  method.  The package maintains a prefab list of such methods (e.g. `playSound`), but it's far from comprehensive.
  If you come up with one or more of these, consider creating a pull request to add them to the stock list.
* Logging.  There's a case to be made to localize the logging, to make it easier for players to read the log,
  but the cost is that it makes it so that you have a hard time reading the log.  It also makes it so that
  the players have a hard time searching forums and so forth for solutions to whatever problem it is that
  they're experiencing.  In a world where machine translation and web searches exist, it's probably better
  to make your logging in the source language.  If you've rolled some of your own logging functions, what
  you can do is convert them to always taking `FormattableString` as an argument (rather than plain string)
  and adding `ArgumentIsCultureInvariant` as an attribute to the method.  Conversion to formattable string 
  as an argument will force you to add a $ in front of all the plain strings you call it with.  It's not a
  great solution, but it feels better than having two different overloads for your logging function.
* Exception messages are a similar story to logging.  It's going to be case-by-case.  If you use exceptions
  within your mod as a means to pass error messages to the user, then they should be localized.  However,
  most exception messages only ever land in the log file, and so are covered by what we said about Logging.
  However, given the case-by-case nature of the thing, it's best to just go ahead and mark the messages
  with an `I` or `L` and not try and automate the messages away.  However, if you want to play it differently,
  you can add the exception's constructor to the `invariantMethods` block.
* Methods and classes that just have a ton of non-localized strings in them that aren't ever going to have any
  localized strings.  It happens.  You can use the `NoStrict` attribute on methods and classes like this
  and it will disable strict-mode just for those methods/classes.
* Recognizable strings.  It could be that there's a string pattern that you use in your code that is
  mechanically identifiable and will never be localizable.  For these, you can write a regular expression
  to recognize them (a .net regular expression with escaping to fit in a json file) and add that to the
  `invariantStringPatterns` array.  Be exceedingly careful with these patterns and make sure they don't
  catch things they shouldn't.  The compiler can get confused and make mistakes with `LF` if these match
  things they shouldn't.

But there will be a broad set of cases that just don't fit into any of these categories and so you will
doubtless have a fair number of `I` and `IF` calls sprinkled through your code.  Hopefully these instances
will have some beneficial effect in highlighting the nature of these strings and maybe making the code
a little more readable rather than less so.

## Usage

### New Translations

Right after you convert your source code to using this, there will be two files in your i18n folder,
`default.json` and `new-language-template.json`.  The usual way for translators to work is to copy
`default.json` to their target language, say `it.json`, and replace all the English strings with
Italian ones.  That still works.  In this model, there are some other options, depending on the
technical sophistication of the translator.

#### Translators that also are comfortable with Visual Studio and GitHub should...

1. Fork/Clone the mod repo
2. Copy `18n/new-language-template.json` to the new language (say `it.edits.json`)
3. Edit `it.edits.json` and make all the `newTarget` properties into translations of `newSource`.
4. Save and compile.
5. Test in the game
6. Commit, push and create a PR.  Your PR should contain only `it.json` as a new file.
7. Before completing the PR, merge from the target branch and ensure that no new changes
   need to be translated.  Look for a new `it.edits.json` file.  If none turns up, you're golden.
   If it does, update the `newTarget` field for all the edits, rebuild, test again, and push.

### Translators that are not at all comfortable with developer tools should...

1. Copy `default.json` in the mod install folder to their language, say `it.json`
2. Translate everything just based on their knowledge of the mod.
3. Test the game
4. Send the author the new `it.json` and specify exactly which version they were working from.

The author will then

1. Check out the main branch at the commit associated with the version the translator was working from.
2. At this point it should build without creating a `it.edits.json` file.
3. Create a branch and commit
4. Merge with the tip
5. Build
6. Check for an `it.edits.json` file.  If it exists, send that to the translator and ask for
   updates to the impacted strings.
7. Copy that file into the build over top of the old one and build again.
8. Once the `it.edits.json` file is gone, you're done.  Else, go back to step 4.

### Updating Translations

Change the "Translations" section on your readme to instruct people interested in updating the
translations to look at the i18n folder for those `.edits.json` file and either send in pull
requests with translations for those things or create pull requests.

### Sure would be nice...

If there was a script that did just the `xx.edits.json` => `xx.json` part of the build.

## Help wanted

This library scratches an itch I've had throughout my career.  Everywhere I work there's a semi-broken
approach to localization, much of it essentially unfixable because of historical, contractual,
or just funding reasons.  With SDV mods, at least in my own mods, I can fix it.  But in any system,
there's room for improvement.  Here are a few areas where somebody could add value.

### Better line number reporting

This represents my first foray into .Net IL, and the API doesn't make it at all clear how to get a good
track of where strings are coming from.  If you know better, please fix my mistakes!

### Actually construct a call graph

Again looking at my IL code (in `LocalizeFromSource\Decompiler.cs`) you see that it's really pretty
stupid - it just looks at the gap between `Ldstr` instructions and the first call it can recognize.
That *seems* to be good enough, but I'd feel a whole lot better if a call-graph could be constructed.

### Roslyn analyzer

I've never written one, but I *believe* it'd be possible to write something that could basically do
all the functions of strict mode within the IDE, prior to compilation.  I believe that would yield a much
better experience.

### Partial usage

I've only thought about this thing from the perspective of somebody starting from scratch where
this library does all the localization work.  It seems like a likely use-case would be more of a
mixed bag where some is still old-school and some is this way.  Is that reasonable?  I don't
really know.  Seems like it should be, but it'd take some thought.

### Better interactions for translators

I think the main problem facing translated mods has has more to do with the culture
of modding than any kind of code problem.  We have enthusiasts creating mods on their own time
(and sometimes abandoning them) and we have enthusiasts contributing translations on their own time
(and sometimes abandoning them).  We need some ways to make the process of contributing translations
to be more easily crowd-sourced.

I also wonder if we could leverage Google Translate to create placeholders until a human has a chance
to review a string.
