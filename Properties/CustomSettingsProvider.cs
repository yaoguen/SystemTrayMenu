﻿// <copyright file="CustomSettingsProvider.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace SystemTrayMenu.Properties
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;

    internal class CustomSettingsProvider : SettingsProvider
    {
        private const string NameOf = "name";
        private const string SerializeAs = "serializeAs";
        private const string Config = "configuration";
        private const string UserSettings = "userSettings";
        private const string Setting = "setting";

        private bool loaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSettingsProvider"/> class.
        /// Loads the file into memory.
        /// </summary>
        public CustomSettingsProvider()
        {
            SettingsDictionary = new Dictionary<string, SettingStruct>();
        }

        /// <summary>
        /// Gets or sets override.
        /// </summary>
        public override string ApplicationName
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.Name;
            }

            set
            {
                // do nothing
            }
        }

        /// <summary>
        /// Gets the setting key this is returning must set before the settings are used.
        /// e.g. <c>Properties.Settings.Default.SettingsKey = @"C:\temp\user.config";</c>.
        /// </summary>
        private string UserConfigPath
        {
            get
            {
                return Path.Combine(
                Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                $"SystemTrayMenu"),
                $"user-{Environment.MachineName}.config");
            }
        }

        /// <summary>
        /// Gets or sets in memory storage of the settings values.
        /// </summary>
        private Dictionary<string, SettingStruct> SettingsDictionary { get; set; }

        /// <summary>
        /// Override.
        /// </summary>
        /// <param name="name">name.</param>
        /// <param name="config">config.</param>
        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(ApplicationName, config);
        }

        /// <summary>
        /// Must override this, this is the bit that matches up the designer properties to the dictionary values.
        /// </summary>
        /// <param name="context">context.</param>
        /// <param name="collection">collection.</param>
        /// <returns>SettingsPropertyValueCollection.</returns>
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            // load the file
            if (!loaded)
            {
                loaded = true;
                LoadValuesFromFile();
            }

            // collection that will be returned.
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();

            // itterate thought the properties we get from the designer, checking to see if the setting is in the dictionary
            foreach (SettingsProperty setting in collection)
            {
                SettingsPropertyValue value = new SettingsPropertyValue(setting);
                value.IsDirty = false;

                // need the type of the value for the strong typing
                var t = Type.GetType(setting.PropertyType.FullName);

                if (SettingsDictionary.ContainsKey(setting.Name))
                {
                    value.SerializedValue = SettingsDictionary[setting.Name].Value;
                    value.PropertyValue = Convert.ChangeType(SettingsDictionary[setting.Name].Value, t, CultureInfo.InvariantCulture);
                }
                else
                {
                    // use defaults in the case where there are no settings yet
                    value.SerializedValue = setting.DefaultValue;
                    value.PropertyValue = Convert.ChangeType(setting.DefaultValue, t, CultureInfo.InvariantCulture);
                }

                values.Add(value);
            }

            return values;
        }

        /// <summary>
        /// Must override this, this is the bit that does the saving to file.  Called when Settings.Save() is called.
        /// </summary>
        /// <param name="context">context.</param>
        /// <param name="collection">collection.</param>
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            // grab the values from the collection parameter and update the values in our dictionary.
            foreach (SettingsPropertyValue value in collection)
            {
                var setting = new SettingStruct()
                {
                    Value = value.PropertyValue == null ? string.Empty : value.PropertyValue.ToString(),
                    Name = value.Name,
                    SerializeAs = value.Property.SerializeAs.ToString(),
                };

                if (!SettingsDictionary.ContainsKey(value.Name))
                {
                    SettingsDictionary.Add(value.Name, setting);
                }
                else
                {
                    SettingsDictionary[value.Name] = setting;
                }
            }

            // now that our local dictionary is up-to-date, save it to disk.
            SaveValuesToFile();
        }

        /// <summary>
        /// Loads the values of the file into memory.
        /// </summary>
        private void LoadValuesFromFile()
        {
            if (!File.Exists(UserConfigPath))
            {
                // if the config file is not where it's supposed to be create a new one.
                CreateEmptyConfig();
            }

            // load the xml
            var configXml = XDocument.Load(UserConfigPath);

            // get all of the <setting name="..." serializeAs="..."> elements.
            var settingElements = configXml.Element(Config).Element(UserSettings).Element(typeof(Properties.Settings).FullName).Elements(Setting);

            // iterate through, adding them to the dictionary, (checking for nulls, xml no likey nulls)
            // using "String" as default serializeAs...just in case, no real good reason.
            foreach (var element in settingElements)
            {
                var newSetting = new SettingStruct()
                {
                    Name = element.Attribute(NameOf) == null ? string.Empty : element.Attribute(NameOf).Value,
                    SerializeAs = element.Attribute(SerializeAs) == null ? "String" : element.Attribute(SerializeAs).Value,
                    Value = element.Value ?? string.Empty,
                };
                SettingsDictionary.Add(element.Attribute(NameOf).Value, newSetting);
            }
        }

        /// <summary>
        /// Creates an empty user.config file...looks like the one MS creates.
        /// This could be overkill a simple key/value pairing would probably do.
        /// </summary>
        private void CreateEmptyConfig()
        {
            var doc = new XDocument();
            var declaration = new XDeclaration("1.0", "utf-8", "true");
            var config = new XElement(Config);
            var userSettings = new XElement(UserSettings);
            var group = new XElement(typeof(Properties.Settings).FullName);
            userSettings.Add(group);
            config.Add(userSettings);
            doc.Add(config);
            doc.Declaration = declaration;
            doc.Save(UserConfigPath);
        }

        /// <summary>
        /// Saves the in memory dictionary to the user config file.
        /// </summary>
        private void SaveValuesToFile()
        {
            // load the current xml from the file.
            var import = XDocument.Load(UserConfigPath);

            // get the settings group (e.g. <Company.Project.Desktop.Settings>)
            var settingsSection = import.Element(Config).Element(UserSettings).Element(typeof(Properties.Settings).FullName);

            // iterate though the dictionary, either updating the value or adding the new setting.
            foreach (var entry in SettingsDictionary)
            {
                var setting = settingsSection.Elements().FirstOrDefault(e => e.Attribute(NameOf).Value == entry.Key);
                if (setting == null)
                {
                    // this can happen if a new setting is added via the .settings designer.
                    var newSetting = new XElement(Setting);
                    newSetting.Add(new XAttribute(NameOf, entry.Value.Name));
                    newSetting.Add(new XAttribute(SerializeAs, entry.Value.SerializeAs));
                    newSetting.Value = entry.Value.Value ?? string.Empty;
                    settingsSection.Add(newSetting);
                }
                else
                {
                    // update the value if it exists.
                    setting.Value = entry.Value.Value ?? string.Empty;
                }
            }

            import.Save(UserConfigPath);
        }

        /// <summary>
        /// Helper struct.
        /// </summary>
        internal struct SettingStruct
        {
            internal string Name;
            internal string SerializeAs;
            internal string Value;
        }
    }
}
