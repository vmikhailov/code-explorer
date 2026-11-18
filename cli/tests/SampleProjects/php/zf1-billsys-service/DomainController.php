<?php
require_once '../library-wl/MregRequest.php';

class Billsys_Model_DbTable_Domains extends Zend_Db_Table_Abstract
{
    protected $_name = 'domains';
}

class Billsys_DomainController extends Zend_Controller_Action
{
    public function renewAction()
    {
        $this->db->table('domain_renewals')->insert(array('domain' => 'example.de'));
        mysql_query("DELETE FROM expired_locks WHERE domain_id = 42");
    }
}
