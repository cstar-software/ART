#!/usr/bin/php
<?php
	$project = "/Users/Shared/Unity/NetworkExample";

	// echo "project name:";
	// $project = fgets(STDIN);
	// print("$project\n");
	// exit;

	$name = basename($project);
	$dir = dirname($project);
	$suffix = "_".time();
	$dest = "$dir/$name$suffix";
	print("copying project...\n");
	passthru("cp -R \"$project\" \"$dest\"");

	$delete = array("Assets/NatCorder", "Assets/NatMic");
	$user = exec("/usr/bin/whoami");
	$trash = "/Users/$user/.Trash";
	foreach ($delete as $name) {
		$path = "$dest/$name";
		if (file_exists($path)) {
			$basename = basename($path);
			passthru("mv \"$path\" \"$trash/$basename$suffix\"\n");
		}
	}
?>