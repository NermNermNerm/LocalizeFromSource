# Localization From Source

This library aims to simplify the process of localizing Stardew Valley mods by allowing the source of truth
for strings to remain in the C# code.

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
