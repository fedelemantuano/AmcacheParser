# Amcache evidence

Amcache evidence describes files, programs, devices and drivers recorded in a Windows Amcache registry hive.

## Language

**Amcache hive**:
The Windows registry hive containing the evidence examined by AmcacheParser. Old-format and new-format hives expose different categories of evidence.

**Program entry**:
An Amcache record describing a program and the file entries linked to it.

**Associated file entry**:
A file entry that the parser links to a program entry found in the same hive.
_Avoid_: Installed file, executed file

**Unassociated file entry**:
A file entry that the parser cannot link to a program entry found in the same hive. This classification alone does not establish whether the file was executed or installed.
_Avoid_: Executed file, uninstalled file

**Record type**:
The category of evidence represented by an exported record, such as a program, an associated file, an unassociated file, a shortcut, a driver or a device.
