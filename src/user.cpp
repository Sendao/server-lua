#include "main.h"

User::User(void)
{
	bQuitting = false;
	outbuf = NULL;
	outbufsz = 0;
	outbufalloc = 0;
	outbufmax = 1500; // MTU
	inbufmax = MSL;
	inbuf = (char*)grabMem( MSL );
	inbufsz = 0;
	sHost = NULL;
	fSock = -1;
	state = 0;
}

User::~User(void)
{
	strmem->
	GS->Free( sHost );
	releaseMem(inbuf);
}


void User::SendMessage( Message *m )
{
	char *buf, *vbuf;
	tnode *n, *nn, *np;
	tlist *l;
	uintptr_t len;
	uint16_t shlen;

	buf = WriteObject(Message::registryid, (void*)m, &len);
	shlen = (uint16_t)len;
	vbuf = (char*)grabMem( shlen + sizeof(uint16_t) );
	*(uint16_t*)vbuf = shlen;//htons( len );
	memcpy( vbuf+sizeof(uint16_t), buf, shlen );
	releaseMem(buf);
	buf=vbuf;

	n = nn = np = NULL;
	for( n = qosbuf->nodes; n; np=n, n = nn ) {
		l = (tlist*)n->data;
		if( l->type < m->priority ) {
			nn = n->next;
			//lprintf("msg: next");
			continue;
		}
		if( l->type == m->priority ) {
			l->PushBack(buf);
			//lprintf("msg: push-back");
			return;
		}
		break;
	}

	l = new tlist;
	l->type = m->priority;
	l->Push(buf);
//	lprintf("msg: push-new");

	if( np ) {
		qosbuf->InsertAfter(np, node_(l));
//		lprintf("insert new %d", m->priority);
	} else {
		qosbuf->Push(l);
//		lprintf("push new %d", m->priority);
	}
	return;
}

void User::Flush( int count )
{
	tnode *n, *nn, *n2, *n2n;
	tlist *l;
	uint16_t shlen;

	//lprintf("flush: %d nodes", qosbuf->count);
	for( n = qosbuf->nodes; n; n = nn ) {
		lprintf("flush-list");
		nn = n->next;
		l = (tlist*)n->data;
		lprintf("flushing list (%d)", l->count);

		for( n2 = l->nodes; n2; n2 = n2n ) {
			n2n = n2->next;

			if( n2->data != NULL ) {
				shlen = *(uint16_t*)(n2->data);
				Output( this, (char*)(n2->data), shlen+sizeof(uint16_t) );
//				lprintf("Package size: %d", len);
//				lprintf("i3: send %d+2 bytes [%d]:[%d]:[%d]", len, ntohs( *(uint16_t*)(n2->data) ), ntohs( *( ((uint16_t*)n2->data) +1 ) ), ntohs( *( ((uint16_t*)n2->data) +2 ) )  );
//				lprintf("int3 scan: %d %d %d", (uint16_t*)(n2->data), *( ((uint16_t*)n2->data) +1 ), *(((uint16_t*)n2->data)+2)   );
				releaseMem(n2->data);
			}
			l->Pull(n2);

			count -= shlen;
			if( count <= 0 ) {
				lprintf("abbreviated");
				return;
			}
		}

		deleteMem(l);

		qosbuf->Pull(n);
	}
}
void User::DoCommand( command_data *cmd )
{
//	lprintf("Cmdid = %d", cmd->cmdid);
	callback_link *cb = LookupAction( cmd->cmdid );
	if( !cb ) return;
	CAction *a = new CAction();
	a->opt = new OMap( cmd->opt );
	cb->func(a, (void*)this);
	deleteMem(a);

}

void User::ProcessMessages( void )
{
	tnode *n, *nn;
	Message *m;
	Object *o;
//	Event *e;
	command_data *c;
//	bool bMoved=false;
	classdef *baseclass;
	void *p;

	lprintf("ProcessMessages()");
	forTSLIST( m, n, messages, Message*, nn ) {
/*		if( m->userid != id ) {
			lprintf("Spoofed userid, breaking message.");
			deleteMem(m);

			messages->Pull(n);
			continue;
		} */

		baseclass = LookupClass(m->type);
		p = ClassFactory(baseclass);
//		lprintf("Message type %hu data scan: %hu %hu %hu (%d %hu)", m->type, ntohs( *(uint16_t*)(m->data) ),ntohs( *(uint16_t*)(m->data+2) ), ntohs( *(uint16_t*)(m->data+4) ), m->type, ntohs( *(uint16_t*)(m->data) ) );
		uintptr_t iSize;
		p = ReadObjectOf( baseclass, m->data, m->size, &iSize );
		if( iSize <= 0 || !p ) {
			lprintf("Failed to ressify type %d", m->type);
			ClassFreeX(p,baseclass);
			deleteMem(m);

			messages->Pull(n);
			continue;
		}
		lprintf("Type = %d, CommandID = %d", m->type, CommandID );
		if( m->type == Object::registryid ) {
			o = (Object*)p;
			o->FinishLoadComplete();
			//!fixme
			//world->objs->PushBack( o );

/*
		} else if( m->type == EventID ) {
//			lprintf("Processing event.");
			e = (Event*)p;
			o = FindObject( this, e->objid );
			if( !o ) {
				lprintf("Bad event: unknown object id %ld.", e->objid);
				deleteMem(e);

			} else {
				o->events->PushBack(e);
			}
*/
		} else if( m->type == CommandID ) {
			c = (command_data*)p;
//			if( c->cmdid == 0 ) bMoved = true;
			lprintf("Run command.");
			DoCommand(c);
			ClassFreeX(p,baseclass);
		} else {
			lprintf("Unknown message type: %d", m->type);
			ClassFreeX(p,baseclass);
		}
		deleteMem(m);

		messages->Pull(n);
	}
}
