<?php include('nav_bar.php'); ?>

<div class="container" style="margin-top:75px">
	<div class="row">
    	<div class="col-md-4">
			<p><?php
if (!isset($_SESSION['steamid'])) {
	echo "<p class=\"bg-warning\">Welcome, Guest. Please <a href=\"/link\">login</a> to access more content.</p>";
} else {
	include 'steamauth/userInfo.php';
	$avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
    echo "<p class=\"bg-success\">Welcome, $profile_name!<br>";
    if (isset($_SESSION['guilds'])) {
        if (isset($_SESSION['user_id'])) {
            $discordid = $_SESSION['user_id'];
			$steamid = $_SESSION['steamid'];
            echo "DiscordID: $discordid<br>";
			echo "SteamID: $steamid</p>";
        } else {
            echo "</p><p class=\"bg-danger\">Can't find Discord user</p>";
        }
    } else {
        echo "</p><p class=\"bg-warning\"><a href=\"link.php\">Link Discord</a></p>";
    }
}
			?></p>
		</div>
	</div>
</div>

	
<?php include('footer.php'); ?>