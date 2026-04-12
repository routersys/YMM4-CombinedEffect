using System;
using System.Collections.Generic;

namespace CombinedEffect.Services.Interfaces;

internal interface IRecentPresetService
{
    IEnumerable<Guid> GetRecentIds();
    void Add(Guid id);
    void Remove(Guid id);
}
