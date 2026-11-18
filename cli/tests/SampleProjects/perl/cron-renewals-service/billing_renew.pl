#!/usr/bin/perl
package DD24::Cron::BillingRenew;

use strict;
use warnings;
use DBI;
use Switch;
use PTF::XClient;
use Mail::Sendmail;

sub process_due_renewals {
    my ($dbh, $mode) = @_;
    switch ($mode) {
        case 'live' {
            my $rows = $dbh->selectall_arrayref(<<SQL);
SELECT subscription_id, customer_id
FROM billing_subscriptions
WHERE next_renewal_date <= NOW()
GROUP BY customer_id
SQL
            $dbh->do("UPDATE billing_subscriptions SET renewal_state = 'LOCKED' WHERE renewal_state = 'DUE'");
            my $xclient = PTF::XClient->new();
            $xclient->send('RenewDomainCommand');
            sendmail(To => 'billing@example.com');
        }
    }
}

1;
