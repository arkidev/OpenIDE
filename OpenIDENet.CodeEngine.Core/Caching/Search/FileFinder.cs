﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace OpenIDENet.CodeEngine.Core.Caching.Search
{
    class FileFinder
    {
        private List<string> _files;
        private List<Project> _projects;

        public FileFinder(List<string> files, List<Project> projects)
        {
            _files = files;
            _projects = projects;
        }

        public List<FileFindResult> Find(string pattern)
        {
            pattern = pattern.ToLower();
            var list = new List<FileFindResult>();
            _projects
                .Where(x => x.Fullpath.ToLower().Contains(pattern)).ToList()
                .ForEach(x => addFile(list, FileFindResultType.Project, x.Fullpath, pattern));
            _files
                .Where(x => x.ToLower().Contains(pattern)).ToList()
                .ForEach(x => addFile(list, FileFindResultType.File, x, pattern));
            return list;
        }

        private void addFile(List<FileFindResult> list, FileFindResultType type, string x, string pattern)
        {
            var start = x.ToLower().LastIndexOf(pattern);
            var nextDirSeparator = x.IndexOf(Path.DirectorySeparatorChar, start + pattern.Length);
            FileFindResult result;
            if (nextDirSeparator != -1)
                result = new FileFindResult(FileFindResultType.Directory, x.Substring(0, nextDirSeparator));
            else
                result = new FileFindResult(type, x);

            if (doesNotContain(list, result))
                list.Add(result);
        }

        private bool doesNotContain(List<FileFindResult> list, FileFindResult result)
        {
            return list.Where(x => x.Type == result.Type && x.File == result.File).Count() == 0;
        }
    }
}
