﻿using GVFS.Common.Git;
using GVFS.Tests.Should;
using GVFS.Virtualization.Projection;
using GVFS.Windows;
using NUnit.Framework;
using ProjFS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GVFS.UnitTests.Windows
{
    [TestFixtureSource(TestRunners)]
    public class ActiveEnumerationTests
    {
        public const string TestRunners = "Runners";

        private static object[] patternMatchers =
        {
            new object[] { new PatternMatcherWrapper(Utils.IsFileNameMatch) },
            new object[] { new PatternMatcherWrapper(WindowsFileSystemVirtualizer.InternalFileNameMatchesFilter) },
        };

        public ActiveEnumerationTests(PatternMatcherWrapper wrapper)
        {
            ActiveEnumeration.SetPatternMatcher(wrapper.Matcher);
        }

        public static object[] Runners
        {
            get { return patternMatchers; }
        }

        [TestCase]
        public void EnumerationHandlesEmptyList()
        {
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(new List<ProjectedFileInfo>()))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(string.Empty);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryList()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1))
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntries()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithEmptyFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1))
            };

            // Test empty string ("") filter
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            // Test null filter
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithWildcardFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1))
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                string filter = "*.*";
                activeEnumeration.TrySaveFilterString(filter).ShouldEqual(true);

                // "*.*" should only match when there is a . in the name
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(filter);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithMatchingFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1))
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("a").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("A").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithNonMatchingFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1))
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                string filter = "b";
                activeEnumeration.TrySaveFilterString(filter).ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(filter);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void CannotSetMoreThanOneFilter()
        {
            string filterString = "*.*";

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(new List<ProjectedFileInfo>()))
            {
                activeEnumeration.TrySaveFilterString(filterString).ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(false);
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(false);
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(false);
                activeEnumeration.GetFilterString().ShouldEqual(filterString);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithEmptyFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            // Test empty string ("") filter
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            // Test null filter
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithWildcardFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo(".txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("D.", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
                new ProjectedFileInfo("E..log", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 6)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 7)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 8)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("*.*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Contains(".")));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("*.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '<' = DOS_STAR, matches 0 or more characters until encountering and matching
            //                 the final . in the name
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("<.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 1));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("?.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '>' = DOS_QM, matches any single character, or upon encountering a period or
            //               end of name string, advances the expression to the end of the
            //               set of contiguous DOS_QMs.
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(">.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length <= 5 && entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E.???").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '"' = DOS_DOT, matches either a . or zero characters beyond name string.
            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("E", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("E", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("B\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("B.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("B", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e.???").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithMatchingFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E.bat").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name == "E.bat"));
            }

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e.bat").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => string.Compare(entry.Name, "e.bat", StringComparison.OrdinalIgnoreCase) == 0));
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithNonMatchingFilter()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                string filter = "g";
                activeEnumeration.TrySaveFilterString(filter).ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(filter);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void SettingFilterAdvancesEnumeratorToMatchingEntry()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");
            }
        }

        [TestCase]
        public void RestartingScanWithFilterAdvancesEnumeratorToNewMatchingEntry()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("a", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("B", size: 0, isFolder:true, sha: Sha1Id.None),
                new ProjectedFileInfo("c", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 5)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");

                activeEnumeration.RestartEnumeration("c");
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("c");
            }
        }

        [TestCase]
        public void RestartingScanWithFilterAdvancesEnumeratorToFirstMatchingEntry()
        {
            List<ProjectedFileInfo> entries = new List<ProjectedFileInfo>()
            {
                new ProjectedFileInfo("C.TXT", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 1)),
                new ProjectedFileInfo("D.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 2)),
                new ProjectedFileInfo("E.txt", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 3)),
                new ProjectedFileInfo("E.bat", size: 0, isFolder:false, sha: new Sha1Id(1, 1, 4)),
            };

            using (ActiveEnumeration activeEnumeration = new ActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");

                activeEnumeration.RestartEnumeration("c*");
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("C.TXT");
            }
        }

        private void ValidateActiveEnumeratorReturnsAllEntries(ActiveEnumeration activeEnumeration, IEnumerable<ProjectedFileInfo> entries)
        {
            activeEnumeration.IsCurrentValid.ShouldEqual(true);

            // activeEnumeration should iterate over each entry in entries
            foreach (ProjectedFileInfo entry in entries)
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entry);
                activeEnumeration.MoveNext();
            }

            // activeEnumeration should no longer be valid after iterating beyond the end of the list
            activeEnumeration.IsCurrentValid.ShouldEqual(false);

            // attempts to move beyond the end of the list should fail
            activeEnumeration.MoveNext().ShouldEqual(false);
        }

        public class PatternMatcherWrapper
        {
            public PatternMatcherWrapper(ActiveEnumeration.FileNamePatternMatcher matcher)
            {
                this.Matcher = matcher;
            }

            public ActiveEnumeration.FileNamePatternMatcher Matcher { get; }
        }
    }
}