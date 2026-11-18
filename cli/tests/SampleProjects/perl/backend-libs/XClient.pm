package PTF::XClient;

use strict;
use warnings;
use DBI;

sub new {
    my ($class, %args) = @_;
    return bless \%args, $class;
}

sub send {
    my ($self, $command, $dbh) = @_;
    if ($dbh) {
        $dbh->do("INSERT INTO mreg_audit_log (command_name, created_at) VALUES ('$command', NOW())");
    }
    return { code => 200, command => $command };
}

1;
