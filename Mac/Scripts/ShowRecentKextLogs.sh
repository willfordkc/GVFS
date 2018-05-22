#TODO: filter on subsystem, not image path
log show --predicate 'senderImagePath CONTAINS "PrjFSKext"' --style syslog --last 10m
