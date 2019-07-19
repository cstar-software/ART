#!/usr/bin/php
<?php
	
	$name = "NetworkExample";
	$cwd = "/Users/Shared/Unity/NetworkExample/Build";
	chdir($cwd);
  $dest = "/Volumes/Roger’s Public Folder";
  
  $zip = false;

  if ($zip) {
    $source = "$cwd/$name.zip";
    passthru("zip -r $name.zip $name.app");
    passthru("rsync -auv --compress --stats \"$source\" \"$dest\"");
  } else {
    $source = "$cwd/$name.app";
    $dest = "/Volumes/Roger’s Public Folder";
    passthru("rsync -auv --compress --stats \"$source\" \"$dest\"");
  }


?>